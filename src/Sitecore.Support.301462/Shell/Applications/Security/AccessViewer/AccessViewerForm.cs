using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Security;
using Sitecore.Security.AccessControl;
using Sitecore.Security.Accounts;
using Sitecore.Shell.Framework;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Shell.Web.UI.WebControls;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls.Ribbons;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Web.UI;
using Sitecore.Collections;
using Sitecore.Data.Managers;
using Sitecore.Resources;
using Sitecore.Security.Domains;
using Sitecore.Workflows;
using System.Collections.Generic;
using System.Linq;
using System.Web.Security;

namespace Sitecore.Support.Shell.Applications.Security.AccessViewer
{
  public class AccessViewerForm:ApplicationForm
  {
    protected DataContext DataContext;

    /// <summary></summary>
    protected Sitecore.Support.Shell.Web.UI.WebControls.AccessViewerTreeView Treeview;

    /// <summary></summary>
    protected DataContext EntityDataContext;

    /// <summary></summary>
    protected Border RibbonPanel;

    /// <summary></summary>
    protected Scrollbox Explanation;

    /// <summary>
    /// Gets or sets the type of the account.
    /// </summary>
    /// <value>The type of the account.</value>
    public AccountType AccountType
    {
      get
      {
        object obj = Context.ClientPage.ServerProperties["AccountType"];
        if (obj == null)
        {
          return AccountType.Unknown;
        }
        return (AccountType)obj;
      }
      set
      {
        Context.ClientPage.ServerProperties["AccountType"] = value;
      }
    }

    /// <summary>
    /// Gets or sets the name of the account.
    /// </summary>
    /// <value>The name of the account.</value>
    public string AccountName
    {
      get
      {
        return StringUtil.GetString(Context.ClientPage.ServerProperties["AccountName"]);
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        Context.ClientPage.ServerProperties["AccountName"] = value;
      }
    }

    /// <summary>
    /// Gets or sets the domain.
    /// </summary>
    /// <value>The domain.</value>
    public string DomainName
    {
      get
      {
        return Context.ClientPage.ServerProperties["DomainName"] as string;
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        Context.ClientPage.ServerProperties["DomainName"] = value;
      }
    }

    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    /// <value>The item ID.</value>
    public string ItemID
    {
      get
      {
        return Context.ClientPage.ServerProperties["ItemID"] as string;
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        Context.ClientPage.ServerProperties["ItemID"] = value;
      }
    }

    /// <summary>
    /// Gets or sets the name of the right.
    /// </summary>
    /// <value>The name of the right.</value>
    public string RightName
    {
      get
      {
        return Context.ClientPage.ServerProperties["RightName"] as string;
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        Context.ClientPage.ServerProperties["RightName"] = value;
      }
    }

    /// <summary>
    /// Handles the message.
    /// </summary>
    /// <param name="message">The message.</param>
    public override void HandleMessage(Message message)
    {
      Assert.ArgumentNotNull(message, "message");
      base.HandleMessage(message);
      Item[] currentItem = this.GetCurrentItem(message);
      if (message.Name == "itemsecurity:changed")
      {
        for (int i = 0; i < currentItem.Length; i++)
        {
          this.Treeview.UpdateNode(currentItem[i]);
        }
        return;
      }
      if (message.Name == "accessviewer:columnschanged")
      {
        UrlString urlString = new UrlString(WebUtil.GetRawUrl());
        if (currentItem != null && currentItem.Length != 0)
        {
          urlString["fo"] = currentItem[0].ID.ToString();
        }
        SheerResponse.SetLocation(urlString.ToString());
        return;
      }
      if (message.Name == "item:load")
      {
        string text = message["id"];
        if (!string.IsNullOrEmpty(text))
        {
          Item item = this.DataContext.GetItem(text);
          if (item != null)
          {
            this.DataContext.SetFolder(item.Uri);
          }
        }
        return;
      }
      CommandContext commandContext = new CommandContext(currentItem);
      if (message.Arguments != null)
      {
        commandContext.Parameters.Add(message.Arguments);
      }
      commandContext.Parameters["domainname"] = this.DomainName;
      commandContext.Parameters["accountname"] = this.AccountName;
      commandContext.Parameters["accounttype"] = this.AccountType.ToString();
      commandContext.Parameters["includeeveryone"] = "1";
      commandContext.Folder = this.DataContext.GetFolder();
      Dispatcher.Dispatch(message, commandContext);
    }

    /// <summary>
    /// Raises the load event.
    /// </summary>
    /// <param name="e">The <see cref="T:System.EventArgs" /> instance containing the event data.</param>
    protected override void OnLoad(EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");
      base.OnLoad(e);
      this.DataContext.Changed += new DataContext.DataContextChangedDelegate(this.DataContext_Changed);
      if (Context.ClientPage.IsEvent)
      {
        return;
      }
      this.DomainName = SecurityUtility.GetDomainName();
      this.DataContext.GetFromQueryString();
      this.DataContext.Parameters = "domain=" + this.DomainName;
      string accountName;
      AccountType accountType;
      SecurityUtility.GetAccountNameAndType(this.DomainName, out accountName, out accountType);
      this.AccountName = accountName;
      this.AccountType = accountType;
      this.Treeview.DomainName = this.DomainName;
      this.Treeview.AccountName = this.AccountName;
      this.Treeview.AccountType = this.AccountType;
      this.Treeview.ColumnNames.Add(0.ToString(), "Name");
      Collection<AccessRight> arg_ED_0 = AccessRightManager.GetAccessRights();
      int num = 1;
      ListString listString = new ListString(Registry.GetValue("/Current_User/AccessViewer/Columns"));
      foreach (AccessRight current in arg_ED_0)
      {
        if (listString.Contains(current.Name) || (listString.Count == 0 && current.IsItemRight))
        {
          this.Treeview.ColumnNames.Add(num.ToString(), current.Title);
          num++;
        }
      }
      this.Explanation.InnerHtml = "<div class=\"scExplanationNothing\">" + Translate.Text("Click an access right for the selected item.") + "</div>";
      this.UpdateRibbon();
    }

    /// <summary>
    /// Explains the specified item ID.
    /// </summary>
    /// <param name="itemID">The item ID.</param>
    /// <param name="rightName">Name of the right.</param>
    protected void Explain(string itemID, string rightName)
    {
      Assert.ArgumentNotNullOrEmpty(itemID, "itemID");
      Assert.ArgumentNotNullOrEmpty(rightName, "rightName");
      Item item = Context.ContentDatabase.GetItem(itemID);
      if (item == null)
      {
        this.Explanation.InnerHtml = "<div class=\"scExplanationNothing\">" + Translate.Text("Selected item does not support access rights.") + "</div>";
        return;
      }
      AccessRight accessRight = null;
      if (rightName != "Inheritance")
      {
        accessRight = AccessRight.FromName(rightName);
        Assert.IsNotNull(accessRight, typeof(AccessRight));
      }
      HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
      new Explainer(this.GetAccount(), item, accessRight).Render(htmlTextWriter);
      this.Explanation.InnerHtml = htmlTextWriter.InnerWriter.ToString();
      this.ItemID = itemID;
      this.RightName = rightName;
      if (item.ID != this.DataContext.GetFolder().ID)
      {
        this.DataContext.SetFolder(item.Uri);
      }
    }

    /// <summary>
    /// Handles the datas changed event.
    /// </summary>
    /// <param name="sender">The sender.</param>
    private void DataContext_Changed(object sender)
    {
      if (!string.IsNullOrEmpty(this.RightName) && this.DataContext.GetFolder() != null)
      {
        this.Explain(this.DataContext.GetFolder().ID.ToString(), this.RightName);
      }
    }

    /// <summary>
    /// Gets the account.
    /// </summary>
    /// <returns>The account.</returns>
    private Account GetAccount()
    {
      Account account = null;
      if (this.AccountType == AccountType.User)
      {
        User expr_17 = User.FromName(this.AccountName, true);
        Assert.IsNotNull(expr_17, typeof(User));
        account = expr_17;
      }
      else if (this.AccountType == AccountType.Role)
      {
        Role expr_3E = Role.FromName(this.AccountName);
        Assert.IsNotNull(expr_3E, typeof(Role));
        account = expr_3E;
      }
      Assert.IsNotNull(account, typeof(Account));
      return account;
    }

    /// <summary>
    /// Gets the current item.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns></returns>
    private Item[] GetCurrentItem(Message message)
    {
      Assert.ArgumentNotNull(message, "message");
      string text = message["id"];
      Item[] selected = this.DataContext.GetSelected();
      if (selected.Length != 0)
      {
        return selected;
      }
      Item folder = this.DataContext.GetFolder();
      if (!string.IsNullOrEmpty(text))
      {
        return new Item[]
        {
                    folder.Database.Items[text, folder.Language]
        };
      }
      return new Item[]
      {
                folder
      };
    }

    /// <summary>
    /// Refreshes the explanation.
    /// </summary>
    protected void RefreshExplain()
    {
      this.Explain(this.ItemID, this.RightName);
    }

    /// <summary>
    /// Updates the ribbon.
    /// </summary>
    private void UpdateRibbon()
    {
      Ribbon ribbon = new Ribbon();
      ribbon.ID = "Ribbon";
      CommandContext commandContext = new CommandContext(this.DataContext.GetFolder());
      ribbon.CommandContext = commandContext;
      ribbon.ShowContextualTabs = false;
      Item item = Context.Database.GetItem("/sitecore/content/Applications/Security/Access Viewer/Ribbon");
      Assert.IsNotNull(item, "/sitecore/content/Applications/Security/Access Viewer/Ribbon");
      commandContext.RibbonSourceUri = item.Uri;
      commandContext.Folder = this.DataContext.GetFolder();
      commandContext.Parameters["domainname"] = this.DomainName;
      commandContext.Parameters["accountname"] = this.AccountName;
      commandContext.Parameters["accounttype"] = this.AccountType.ToString();
      this.RibbonPanel.InnerHtml = HtmlUtil.RenderControl(ribbon);
    }
  }

  internal class Explainer
  {
    private readonly AccessRight _accessRight;

    private readonly Account _account;

    private readonly Item _item;

    /// <summary>
    /// Gets the access right.
    /// </summary>
    /// <value>The access right.</value>
    private AccessRight AccessRight
    {
      get
      {
        return this._accessRight;
      }
    }

    /// <summary>
    /// Gets the account.
    /// </summary>
    /// <value>The account.</value>
    private Account Account
    {
      get
      {
        return this._account;
      }
    }

    /// <summary>
    /// Gets the item.
    /// </summary>
    /// <value>The item.</value>
    private Item Item
    {
      get
      {
        return this._item;
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Security.AccessViewer.Explainer" /> class.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="item">The item.</param>
    /// <param name="accessRight">The access right.</param>
    public Explainer(Account account, Item item, AccessRight accessRight)
    {
      Assert.ArgumentNotNull(account, "account");
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(accessRight, "accessRight");
      this._account = account;
      this._item = item;
      this._accessRight = accessRight;
    }

    /// <summary>
    /// Renders the specified output.
    /// </summary>
    /// <param name="output">The output.</param>
    public void Render(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      string title = this.AccessRight.Title;
      this.RenderHeader(output, title);
      this.RenderWarning(output);
      output.Write("<div class=\"scExplanationSections\">");
      if (this._accessRight != null)
      {
        this.RenderSecurity(output);
        this.RenderWorkflow(output);
        this.RenderLanguage(output);
      }
      output.Write("</div>");
    }

    /// <summary>
    /// Gets the related accounts.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The related accounts.</returns>
    private AccountList GetRelatedAccounts(Account account)
    {
      Assert.ArgumentNotNull(account, "account");
      Set<Account> set = Set<Account>.Create(new Account[]
      {
                account
      });
      User user = account as User;
      if (user != null)
      {
        if (RolesInRolesManager.RolesInRolesSupported)
        {
          set.AddRange(RolesInRolesManager.GetRolesForUser(user, true).ToArray<Role>());
        }
        else
        {
          set.AddRange(Explainer.GetRoles(Roles.GetRolesForUser(user.Name)));
        }
      }
      Role role = account as Role;
      if (role != null && RolesInRolesManager.RolesInRolesSupported)
      {
        set.AddRange(RolesInRolesManager.GetRolesForRole(role, true).ToArray<Role>());
      }
      if (account.Equals(this._item.Security.GetOwner()))
      {
        set.Add(RolesInRolesManager.GetCreatorOwnerRole());
      }
      set.Add(RolesInRolesManager.GetEveryoneRole());
      Domain domain = account.Domain;
      if (domain != null)
      {
        Role everyoneRole = domain.GetEveryoneRole();
        if (everyoneRole != null)
        {
          set.Add(everyoneRole);
        }
      }
      return new AccountList(new List<Account>(set));
    }

    /// <summary>
    /// Gets the roles.
    /// </summary>
    /// <param name="roleNames">The role names.</param>
    /// <returns>The roles.</returns>
    private static Role[] GetRoles(IEnumerable<string> roleNames)
    {
      Assert.ArgumentNotNull(roleNames, "roleNames");
      List<Role> list = new List<Role>();
      foreach (string current in roleNames)
      {
        list.Add(Role.FromName(current));
      }
      return list.ToArray();
    }

    /// <summary>
    /// Renders the explanation accounts.
    /// </summary>
    /// <param name="output">The output.</param>
    private void RenderSecurity(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      Explainer.RenderSectionStart(output, Translate.Text("Security"), "network/16x16/id_card_view.png");
      AccessResult expr_42 = AuthorizationManager.GetAccess(SecurityUtil.GetSecurable(this.Item, this.AccessRight), this.Account, this.AccessRight);
      string text = expr_42.Explanation.Text;
      if (expr_42.Permission == AccessPermission.NotSet && !this.AccessRight.IsFieldRight)
      {
        text = "Access to this Item is denied as no access rule allows access.";
      }
      if (!string.IsNullOrEmpty(text))
      {
        output.Write("<div class=\"scExplanationSecurityText\">");
        if (this._account.Equals(this._item.Security.GetOwner()))
        {
          output.Write(Translate.Text("{0} is the owner of this item.", new object[]
          {
                        this.Account.Name
          }));
          output.Write("<br/>");
        }
        output.Write(Translate.Text(text));
        output.Write("</div>");
      }
      output.Write("<table class=\"scExplanationSecurityTable\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\">");
      output.Write("<tr class=\"scExplanationSecurityTableHeaderRow\">");
      output.Write("<td class=\"scExplanationSecurityTableHeaderCell\">");
      output.Write(Translate.Text("Item"));
      output.Write("</td>");
      output.Write("<td colspan=\"2\" class=\"scExplanationSecurityTableHeaderCell\">");
      output.Write(Translate.Text("Security"));
      output.Write("</td>");
      output.Write("</tr>");
      this.RenderSecurityAncestors(output);
      output.Write("<tr>");
      output.Write("<td colspan=\"3\" class=\"scExplanationSecurityTableBottom\">");
      output.Write(Images.GetSpacer(1, 1));
      output.Write("</td>");
      output.Write("</tr>");
      output.Write("</table>");
      Explainer.RenderSectionEnd(output);
    }

    /// <summary>
    /// Renders the security account.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    /// <param name="account">The account.</param>
    /// <param name="propagationType">The propagation type</param>
    /// <param name="skipItemName">if set to <c>true</c> this instance is skip item name.</param>
    /// <returns>The security account.</returns>
    /// m&gt;
    private bool RenderSecurityAccountAccess(HtmlTextWriter output, Item item, Account account, PropagationType propagationType, bool skipItemName)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(account, "account");
      AccessRuleCollection accessRules = item.Security.GetAccessRules();
      Assert.IsNotNull(accessRules, typeof(AccessRuleCollection));
      AccessPermission accessPermission = accessRules.Helper.GetAccessPermission(account, this.AccessRight, propagationType);
      if (accessPermission == AccessPermission.NotSet)
      {
        return false;
      }
      if (!accessRules.Helper.ContainsAccount(account))
      {
        return false;
      }
      HtmlTextWriter expr_71 = new HtmlTextWriter(new StringWriter());
      Explainer.RenderSelector(expr_71, accessPermission, false);
      string selector = expr_71.InnerWriter.ToString();
      Explainer.RenderSecurityTreeNode(output, item, account.Name, account.AccountType, selector, skipItemName);
      return true;
    }

    /// <summary>
    /// Renders the security account inheritance.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    /// <param name="account">The account.</param>
    /// <param name="propagationType">Type of the propagation.</param>
    /// <param name="skipItemName">if set to <c>true</c> [skip item name].</param>
    /// <returns>The security account inheritance.</returns>
    private bool RenderSecurityAccountInheritance(HtmlTextWriter output, Item item, Account account, PropagationType propagationType, bool skipItemName)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(account, "account");
      AccessRuleCollection accessRules = item.Security.GetAccessRules();
      Assert.IsNotNull(accessRules, typeof(AccessRuleCollection));
      InheritancePermission inheritanceRestriction = accessRules.Helper.GetInheritanceRestriction(account, this.AccessRight, propagationType);
      if (inheritanceRestriction == InheritancePermission.NotSet)
      {
        return false;
      }
      if (!accessRules.Helper.ContainsAccount(account))
      {
        return false;
      }
      AccessPermission permission = AccessPermission.NotSet;
      switch (inheritanceRestriction)
      {
        case InheritancePermission.NotSet:
          permission = AccessPermission.NotSet;
          break;
        case InheritancePermission.Allow:
          permission = AccessPermission.Allow;
          break;
        case InheritancePermission.Deny:
          permission = AccessPermission.Deny;
          break;
      }
      HtmlTextWriter expr_99 = new HtmlTextWriter(new StringWriter());
      Explainer.RenderSelector(expr_99, permission, false);
      string selector = expr_99.InnerWriter.ToString();
      Explainer.RenderSecurityTreeNode(output, item, account.Name + " " + Translate.Text("[Inheritance]"), account.AccountType, selector, skipItemName);
      return true;
    }

    /// <summary>
    /// Renders the item.
    /// </summary>
    /// <param name="output">The output.</param>
    private void RenderSecurityAncestors(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      Item[] ancestors = this.Item.Axes.GetAncestors();
      for (int i = 0; i < ancestors.Length; i++)
      {
        Item item = ancestors[i];
        this.RenderSecurityItem(output, item, PropagationType.Descendants);
      }
      this.RenderSecurityItem(output, this.Item, PropagationType.Entity);
    }

    /// <summary>
    /// Renders the security item.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    /// <param name="propagationType">The propagation type</param>
    private void RenderSecurityItem(HtmlTextWriter output, Item item, PropagationType propagationType)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      ReadOnlyList<Account> arg_24_0 = this.GetRelatedAccounts(this.Account);
      bool flag = false;
      foreach (Account account in arg_24_0)
      {
        flag |= this.RenderSecurityAccountAccess(output, item, account, propagationType, flag);
        flag |= this.RenderSecurityAccountInheritance(output, item, account, propagationType, flag);
      }
      if (!flag)
      {
        Explainer.RenderSecurityTreeNode(output, item, "&nbsp;", AccountType.Unknown, Images.GetImage(Themes.MapTheme("Images/Security/NA.gif"), 32, 16, "absmiddle"), false);
      }
    }

    /// <summary>
    /// Renders the explanation tree node.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    /// <param name="accountName">The account.</param>
    /// <param name="accountType">Type of the account.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="skipItemName">if set to <c>true</c> this instance is has account.</param>
    private static void RenderSecurityTreeNode(HtmlTextWriter output, Item item, string accountName, AccountType accountType, string selector, bool skipItemName)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNullOrEmpty(accountName, "accountName");
      Assert.ArgumentNotNullOrEmpty(selector, "selector");
      int level = item.Axes.Level;
      output.Write("<tr class=\"scExplanationSecurityTableRow\">");
      output.Write("<td class=\"scExplanationSecurityTableItem\" style=\"padding:0px 4px 0px " + (level * 16 + 12) + "px\">");
      if (!skipItemName)
      {
        output.Write(Images.GetImage(item.Appearance.Icon, 16, 16, "absmiddle", "0px 4px 0px 0px"));
        output.Write(item.GetUIDisplayName());
      }
      else
      {
        output.Write(Images.GetSpacer(1, 1));
      }
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableSelector\">");
      output.Write(selector);
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableAccount\">");
      bool flag = accountType != AccountType.Unknown && !item.Appearance.ReadOnly && (item.Access.CanWrite() || item.Access.CanAdmin());
      if (flag)
      {
        string text = string.Concat(new object[]
        {
                    "security:openitemsecurityeditor(id=",
                    item.ID,
                    ",ac=",
                    accountName,
                    ",at=",
                    accountType,
                    ")"
        });
        output.Write("<a href=\"#\" onclick='javascript:return scForm.postRequest(\"\",\"\",\"\"," + StringUtil.EscapeJavascriptString(text) + ")'>");
      }
      output.Write(accountName);
      if (flag)
      {
        output.Write("</a>");
      }
      output.Write("</td>");
      output.Write("</tr>");
    }

    /// <summary>
    /// Renders the messages.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="messages">The messages.</param>
    /// <param name="messageClass">The message class.</param>
    private static void RenderMessages(HtmlTextWriter output, List<string> messages, string messageClass)
    {
      if (messages.Count == 0)
      {
        return;
      }
      output.Write("<div class=\"scMessageBar {0}\">", messageClass);
      output.Write("<div class=\"scMessageBarIcon\"></div>");
      output.Write("<div class=\"scMessageBarTextContainer\">");
      foreach (string current in messages)
      {
        output.Write("<div class=\"scMessageBarText\">{0}</div>", current);
      }
      output.Write("</div>");
      output.Write("</div>");
    }

    /// <summary>
    /// Renders the explanation header.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="rightTitle">The right title.</param>
    private void RenderHeader(HtmlTextWriter output, string rightTitle)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNullOrEmpty(rightTitle, "rightTitle");
      output.Write("<div class=\"scExplanationHeader\">");
      output.Write("<div class=\"scExplanationHeaderName\">");
      output.Write(Translate.Text("The {0} access rights for the {1} item", new object[]
      {
                Translate.Text(rightTitle),
                this.Item.GetUIDisplayName()
      }));
      output.Write("</div>");
      output.Write("</div>");
    }

    /// <summary>
    /// Renders the explanation accounts.
    /// </summary>
    /// <param name="output">The output.</param>
    private void RenderWarning(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      List<string> list = new List<string>();
      if (this.Item.Security.GetAccessRules().Helper.HasSpecializedInheritance())
      {
        list.Add(Translate.Text("The item has individial inheritance rules set for each permission."));
      }
      List<string> list2 = new List<string>();
      if (this.Item.Appearance.ReadOnly)
      {
        list2.Add(Translate.Text("The item is protected."));
      }
      if (this.Item.Locking.IsLocked())
      {
        list2.Add(Translate.Text("Locked by '{0}'.", new object[]
        {
                    this.Item.Locking.GetOwnerWithoutDomain()
        }));
      }
      Explainer.RenderMessages(output, list, "scWarning");
      Explainer.RenderMessages(output, list2, "scInformation");
    }

    /// <summary>
    /// Renders the section end.
    /// </summary>
    /// <param name="output">The output.</param>
    private static void RenderSectionEnd(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      output.Write("</div>");
    }

    /// <summary>
    /// Renders the section start.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="title">The title.</param>
    /// <param name="icon">The icon.</param>
    private static void RenderSectionStart(HtmlTextWriter output, string title, string icon)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNullOrEmpty(title, "title");
      Assert.ArgumentNotNullOrEmpty(icon, "icon");
      output.Write("<div class=\"scExplanationSectionHeader\">");
      output.Write(title);
      output.Write("</div>");
      output.Write("<div class=\"scExplanationSection\">");
    }

    /// <summary>
    /// Renders the selector.
    /// </summary>
    /// <param name="permission">The permission.</param>
    /// <param name="small">if set to <c>true</c> this instance is small.</param>
    /// <param name="innerWriter">writer</param>
    private static void RenderSelector(HtmlTextWriter innerWriter, AccessPermission permission, bool small)
    {
      Assert.ArgumentNotNull(innerWriter, "innerWriter");
      ImageBuilder imageBuilder = new ImageBuilder();
      imageBuilder.Width = 16;
      imageBuilder.Height = (small ? 8 : 16);
      imageBuilder.Align = "absmiddle";
      string str = small ? "small" : "large";
      if (permission == AccessPermission.Allow)
      {
        imageBuilder.Src = "Images/Security/" + str + "_allow_enabled.gif";
      }
      else
      {
        imageBuilder.Src = "Images/Security/" + str + "_allow_disabled.gif";
      }
      innerWriter.Write(imageBuilder.ToString());
      if (permission == AccessPermission.Deny)
      {
        imageBuilder.Src = "Images/Security/" + str + "_deny_enabled.gif";
      }
      else
      {
        imageBuilder.Src = "Images/Security/" + str + "_deny_disabled.gif";
      }
      innerWriter.Write(imageBuilder.ToString());
    }

    /// <summary>
    /// Renders the language.
    /// </summary>
    /// <param name="output">The output.</param>
    private void RenderLanguage(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
      foreach (Language current in LanguageManager.GetLanguages(this.Item.Database))
      {
        this.RenderLanguage(htmlTextWriter, current);
      }
      string value = htmlTextWriter.InnerWriter.ToString();
      if (string.IsNullOrEmpty(value))
      {
        return;
      }
      Explainer.RenderSectionStart(output, Translate.Text("Language"), "flags/16x16/flag_generic.png");
      output.Write("<table class=\"scExplanationSecurityTable\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\">");
      output.Write("<tr class=\"scExplanationSecurityTableHeaderRow\">");
      output.Write("<td class=\"scExplanationSecurityTableHeaderCell\">");
      output.Write(Translate.Text("Language"));
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableHeaderCell\">");
      output.Write(Translate.Text("Read"));
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableHeaderCell\">");
      output.Write(Translate.Text("Write"));
      output.Write("</td>");
      output.Write("</tr>");
      output.Write(value);
      output.Write("<tr>");
      output.Write("<td colspan=\"3\" class=\"scExplanationSecurityTableBottom\">");
      output.Write(Images.GetSpacer(1, 1));
      output.Write("</td>");
      output.Write("</tr>");
      output.Write("</table>");
      Explainer.RenderSectionEnd(output);
    }

    /// <summary>
    /// Renders the language.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="language">The language.</param>
    private void RenderLanguage(HtmlTextWriter output, Language language)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(language, "language");
      Item item = this.Item.Database.GetItem(this.Item.ID, language);
      if (item == null)
      {
        return;
      }
      bool flag = item.Access.CanReadLanguage();
      bool flag2 = item.Access.CanWriteLanguage();
      if (flag & flag2)
      {
        return;
      }
      output.Write("<tr class=\"scExplanationSecurityTableRow\">");
      output.Write("<td class=\"scExplanationSecurityTableItem\" style=\"padding:2px 4px 2px 4px\">");
      new ImageBuilder
      {
        Src = language.GetIcon(this.Item.Database),
        Width = 16,
        Height = 16,
        Margin = "0px 4px 0px 0px",
        Align = "absmiddle"
      }.Render(output);
      output.Write(language.GetDisplayName());
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableSelector\" style=\"padding:0px 4px 0px 4px\">");
      AccessPermission permission = flag ? AccessPermission.NotSet : AccessPermission.Deny;
      Explainer.RenderSelector(output, permission, false);
      output.Write("</td>");
      output.Write("<td class=\"scExplanationSecurityTableSelector\" style=\"padding:0px 4px 0px 4px\">");
      AccessPermission permission2 = flag2 ? AccessPermission.NotSet : AccessPermission.Deny;
      Explainer.RenderSelector(output, permission2, false);
      output.Write("</td>");
      output.Write("</tr>");
    }

    /// <summary>
    /// Renders the explanation accounts.
    /// </summary>
    /// <param name="output">The output.</param>
    private void RenderWorkflow(HtmlTextWriter output)
    {
      Assert.ArgumentNotNull(output, "output");
      IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
      if (workflowProvider == null)
      {
        return;
      }
      IWorkflow workflow = workflowProvider.GetWorkflow(this.Item);
      if (workflow == null)
      {
        return;
      }
      WorkflowState state = workflow.GetState(this.Item);
      if (state == null)
      {
        return;
      }
      Explainer.RenderSectionStart(output, Translate.Text("Workflow"), "network/16x16/outbox.png");
      output.Write("<div class=\"scExplanationWorkflow\">");
      output.Write(Translate.Text("Workflow:"));
      output.Write("&nbsp;<a href=\"#\" onclick=\"javascript:return scForm.postRequest('','','','item:load(id=" + workflow.WorkflowID + ")')\">");
      output.Write(Translate.Text(workflow.Appearance.DisplayName));
      output.Write("</a></div>");
      output.Write("<div class=\"scExplanationWorkflowState\">");
      output.Write(Translate.Text("State:"));
      output.Write("&nbsp;<a href=\"#\" onclick=\"javascript:return scForm.postRequest('','','','item:load(id=" + state.StateID + ")')\">");
      output.Write(Translate.Text(state.DisplayName));
      output.Write("</a></div>");
      Item item = this.Item.Database.GetItem(state.StateID);
      AccessPermission permission = (item != null && AuthorizationManager.IsAllowed(item, AccessRight.WorkflowStateWrite, this.Account)) ? AccessPermission.Allow : AccessPermission.Deny;
      output.Write("<div class=\"scExplanationWorkflowStateRight\">");
      output.Write(Translate.Text(AccessRight.WorkflowStateWrite.Title));
      output.Write(": ");
      Explainer.RenderSelector(output, permission, false);
      output.Write("</div>");
      permission = ((item != null && AuthorizationManager.IsAllowed(item, AccessRight.WorkflowStateDelete, this.Account)) ? AccessPermission.Allow : AccessPermission.Deny);
      output.Write("<div class=\"scExplanationWorkflowStateRight\">");
      output.Write(Translate.Text(AccessRight.WorkflowStateDelete.Title));
      output.Write(": ");
      Explainer.RenderSelector(output, permission, false);
      output.Write("</div>");
      Explainer.RenderSectionEnd(output);
    }
  }
}