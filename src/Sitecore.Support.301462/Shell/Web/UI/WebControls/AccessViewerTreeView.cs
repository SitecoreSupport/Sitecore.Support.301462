using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Resources;
using Sitecore.Security;
using Sitecore.Security.AccessControl;
using Sitecore.Security.Accounts;
using Sitecore.SecurityModel;
using Sitecore.Text;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Workflows;
using System;
using System.IO;
using System.Web.UI;


namespace Sitecore.Support.Shell.Web.UI.WebControls
{
  public class AccessViewerTreeView:DataTreeview
  {
    private class RenderRightParams
    {
      private Item _item;

      private bool _applies;

      private string _rightName;

      private string _rightTitle;

      private Account _account;

      private bool isAllowed;

      private Item _securitySetter;

      private bool _isWorkflowAllowed;

      /// <summary>
      /// Gets the item.
      /// </summary>
      /// <value>The item.</value>
      public Item Item
      {
        get
        {
          return this._item;
        }
        set
        {
          this._item = value;
        }
      }

      /// <summary>
      /// Gets a value indicating whether this <see cref="T:Sitecore.Shell.Web.UI.WebControls.AccessViewerTreeview.RenderRightParams" /> is applies.
      /// </summary>
      /// <value><c>true</c> if applies; otherwise, <c>false</c>.</value>
      public bool Applies
      {
        get
        {
          return this._applies;
        }
        set
        {
          this._applies = value;
        }
      }

      /// <summary>
      /// Gets the name of the right.
      /// </summary>
      /// <value>The name of the right.</value>
      public string RightName
      {
        get
        {
          return this._rightName;
        }
        set
        {
          this._rightName = value;
        }
      }

      /// <summary>
      /// Gets the right title.
      /// </summary>
      /// <value>The right title.</value>
      public string RightTitle
      {
        get
        {
          return this._rightTitle;
        }
        set
        {
          this._rightTitle = value;
        }
      }

      /// <summary>
      /// Gets the account.
      /// </summary>
      /// <value>The account.</value>
      public Account Account
      {
        get
        {
          return this._account;
        }
        set
        {
          Assert.ArgumentNotNull(value, "value");
          this._account = value;
        }
      }

      /// <summary>
      /// Gets or sets a value indicating whether this instance is allowed.
      /// </summary>
      /// <value>
      /// 	<c>true</c> if this instance is allowed; otherwise, <c>false</c>.
      /// </value>
      public bool IsAllowed
      {
        get
        {
          return this.isAllowed;
        }
        set
        {
          this.isAllowed = value;
        }
      }

      /// <summary>
      /// Gets or sets the setter.
      /// </summary>
      /// <value>The setter.</value>
      public Item SecuritySetter
      {
        get
        {
          return this._securitySetter;
        }
        set
        {
          this._securitySetter = value;
        }
      }

      /// <summary>
      /// Gets or sets a value indicating whether this instance is workflow allowed.
      /// </summary>
      /// <value>
      /// 	<c>true</c> if this instance is workflow allowed; otherwise, <c>false</c>.
      /// </value>
      public bool IsWorkflowAllowed
      {
        get
        {
          return this._isWorkflowAllowed;
        }
        set
        {
          this._isWorkflowAllowed = value;
        }
      }
    }

    private enum AccessState
    {
      Inherited,
      Allow,
      Deny
    }

    /// <summary>
    /// Gets or sets the entity IDs.
    /// </summary>
    /// <value>The entity IDs.</value>
    public string AccountName
    {
      get
      {
        return base.GetViewStateString("AccountName");
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        base.SetViewStateString("AccountName", value);
      }
    }

    /// <summary>
    /// Gets or sets the type of the account.
    /// </summary>
    /// <value>The type of the account.</value>
    public AccountType AccountType
    {
      get
      {
        object viewStateProperty = base.GetViewStateProperty("AccountType", null);
        if (viewStateProperty == null)
        {
          return AccountType.Unknown;
        }
        return (AccountType)viewStateProperty;
      }
      set
      {
        base.SetViewStateProperty("AccountType", value, null);
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
        return base.GetViewStateString("DomainName");
      }
      set
      {
        Assert.ArgumentNotNullOrEmpty(value, "value");
        base.SetViewStateString("DomainName", value);
      }
    }

    /// <summary>
    /// Updates the node.
    /// </summary>
    /// <param name="item">The item.</param>
    public void UpdateNode(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      DataTreeNode dataTreeNode = base.GetNodeByItemID(item.Paths.LongID) as DataTreeNode;
      Assert.IsNotNull(dataTreeNode, typeof(TreeNode));
      this.UpdateColumnValues(dataTreeNode, item);
      this.RefreshNode(dataTreeNode);
    }

    /// <summary>
    /// Gets the tree node.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="parent">The parent.</param>
    /// <returns></returns>
    protected override TreeNode GetTreeNode(Item item, System.Web.UI.Control parent)
    {
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(parent, "parent");
      DataTreeNode dataTreeNode = new DataTreeNode();
      parent.Controls.Add(dataTreeNode);
      string uniqueID = GetUniqueID("T");
      dataTreeNode.Expandable = item.HasChildren;
      dataTreeNode.Expanded = false;
      dataTreeNode.Header = item.DisplayName;
      dataTreeNode.Icon = item.Appearance.Icon;
      dataTreeNode.ID = uniqueID;
      dataTreeNode.ItemID = item.Paths.LongID;
      dataTreeNode.ToolTip = item.Name;
      dataTreeNode.DataContext = this.DataContext;
      if (!UIUtil.IsIE(8))
      {
        dataTreeNode.Attributes["onmouseover"] = "javascript: $(this).addClassName('hover');";
        dataTreeNode.Attributes["onmouseout"] = "javascript: $(this).removeClassName('hover');";
      }
      this.UpdateColumnValues(dataTreeNode, item);
      return dataTreeNode;
    }

    /// <summary>
    /// Gets the context menu.
    /// </summary>
    /// <param name="where">The position.</param>
    protected override void GetContextMenu(string where)
    {
      Assert.ArgumentNotNull(where, "where");
      string text = Sitecore.Context.ClientPage.ClientRequest.Source;
      Menu menu = new Menu();
      if (text == this.ID)
      {
        menu.Add("__SelectColumns", "Select Columns", "Business/16x16/column.png", string.Empty, "accessviewer:selectcolumns", false, string.Empty, MenuItemType.Normal);
      }
      else
      {
        if (text.IndexOf("_", StringComparison.InvariantCulture) >= 0)
        {
          text = text.Substring(0, text.LastIndexOf("_", StringComparison.InvariantCulture));
        }
        DataTreeNode dataTreeNode = base.FindNode(text) as DataTreeNode;
        if (dataTreeNode == null)
        {
          return;
        }
        menu.Add("__Refresh", "Refresh", "Office/16x16/refresh.png", string.Empty, "Treeview.Refresh(\"" + dataTreeNode.ID + "\")", false, string.Empty, MenuItemType.Normal);
      }
      
      SheerResponse.ShowContextMenu(Sitecore.Context.ClientPage.ClientRequest.Control, where, menu);
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
        User user = User.FromName(this.AccountName, true);
        Assert.IsNotNull(user, typeof(User));
        account = user;
      }
      else if (this.AccountType == AccountType.Role)
      {
        Role role = Role.FromName(this.AccountName);
        Assert.IsNotNull(role, typeof(Role));
        account = role;
      }
      Assert.IsNotNull(account, typeof(Account));
      return account;
    }

    /// <summary>
    /// Gets the security setter.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="accessRight">The access right.</param>
    /// <param name="account">The account.</param>
    /// <returns>The security setter.</returns>
    private Item GetSecuritySetter(Item item, AccessRight accessRight, Account account)
    {
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(accessRight, "accessRight");
      Assert.ArgumentNotNull(account, "account");
      if (item.RuntimeSettings.BrowseOnly)
      {
        return null;
      }
      if (AuthorizationManager.IsDenied(item, accessRight, account))
      {
        return item;
      }
      Item parent = ItemManager.GetParent(item, SecurityCheck.Disable);
      if (parent != null)
      {
        return this.GetSecuritySetter(parent, accessRight, account);
      }
      return null;
    }

    /// <summary>
    /// Adds the right.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="parameters">The parameters.</param>
    private static void RenderRight(HtmlTextWriter output, RenderRightParams parameters)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(parameters, "parameters");
      string value = "scAccessViewerAllowed";
      AccessState accessState = AccessState.Allow;
      if (!parameters.IsAllowed)
      {
        Item securitySetter = parameters.SecuritySetter;
        accessState = AccessState.Deny;
        if (securitySetter != null)
        {
          if (securitySetter.ID == parameters.Item.ID)
          {
            value = "scAccessViewerSecurityDenied";
          }
          else
          {
            value = "scAccessViewerSecurityInheritedDenied";
          }
        }
        else
        {
          value = "scAccessViewerSecurityInheritedDenied";
        }
      }
      if (!parameters.IsWorkflowAllowed)
      {
        value = "scAccessViewerWorkflowDenied";
        accessState = AccessState.Deny;
      }
      if (parameters.Applies)
      {
        output.Write("<a href=\"#\" class=\"");
        output.Write(value);
        output.Write(" scAccessViewerRight\" onclick='javascript: $(this).focus() ; return scForm.postRequest(\"\",\"\",\"\"," + StringUtil.EscapeJavascriptString(string.Concat(new object[]
        {
                    "Explain(\"",
                    parameters.Item.ID,
                    "\",\"",
                    parameters.RightName,
                    "\",null,true)"
        })) + ")'>");
       RenderSelector(output, parameters.Item, parameters.RightName, accessState, false);
      }
      else
      {
        output.Write("<span class=\">");
        output.Write(value);
        output.Write(" scAccessViewerRight\">");
        output.Write(Images.GetImage(Themes.MapTheme("Images/Security/NA.gif"), 32, 16, "absmiddle"));
      }
      output.Write("&nbsp;");
      output.Write(Translate.Text(parameters.RightTitle));
      if (parameters.Applies)
      {
        output.Write("</a>");
        return;
      }
      output.Write("</span>");
    }

    /// <summary>
    /// Updates the column values.
    /// </summary>
    /// <param name="treeNode">The tree node.</param>
    /// <param name="item">The item.</param>
    private void UpdateColumnValues(DataTreeNode treeNode, Item item)
    {
      Assert.ArgumentNotNull(treeNode, "treeNode");
      Assert.ArgumentNotNull(item, "item");
      Account account = this.GetAccount();
      Assert.IsNotNull(account, typeof(Account));
      AccessRightCollection accessRights = AccessRightManager.GetAccessRights();
      Assert.IsNotNull(accessRights, typeof(AccessRightCollection));
      int num = 1;
      ListString listString = new ListString(Registry.GetValue("/Current_User/AccessViewer/Columns"));
      try
      {
        foreach (AccessRight current in accessRights)
        {
          if (listString.Contains(current.Name) || (listString.Count == 0 && current.IsItemRight))
          {
            WorkflowContext workflow = Sitecore.Context.Workflow;
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            RenderRightParams renderRightParams = new RenderRightParams();
            renderRightParams.Account = account;
            ISecurable securable = SecurityUtil.GetSecurable(item, current);
            renderRightParams.Applies = current.AppliesTo(securable);
            renderRightParams.IsAllowed = AuthorizationManager.IsAllowed(securable, current, account);
            renderRightParams.SecuritySetter = this.GetSecuritySetter(item, current, account);
            renderRightParams.Item = item;
            renderRightParams.RightName = current.Name;
            renderRightParams.RightTitle = current.Title;
            if (workflow != null)
            {
              renderRightParams.IsWorkflowAllowed = workflow.IsAllowed(current, item);
            }
            RenderRight(htmlTextWriter, renderRightParams);
            string value = htmlTextWriter.InnerWriter.ToString();
            if (string.IsNullOrEmpty(value))
            {
              value = Translate.Text("Unable to resolve the user or role.");
            }
            treeNode.ColumnValues[num.ToString()] = value;
            num++;
          }
        }
      }
      finally
      {

      }
    }

    /// <summary>
    /// Renders the selector.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="item">The item.</param>
    /// <param name="rightName">Name of the right.</param>
    /// <param name="accessState">State of the access.</param>
    /// <param name="small">if set to <c>true</c> this instance is small.</param>
    private static void RenderSelector(HtmlTextWriter output, Item item, string rightName, AccessState accessState, bool small)
    {
      Assert.ArgumentNotNull(output, "output");
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNullOrEmpty(rightName, "rightName");
      ImageBuilder imageBuilder = new ImageBuilder();
      imageBuilder.Width = 16;
      imageBuilder.Height = (small ? 8 : 16);
      imageBuilder.Align = "absmiddle";
      string str = small ? "small" : "large";
      if (accessState == AccessState.Allow)
      {
        imageBuilder.Src = "Images/Security/" + str + "_allow_enabled.gif";
      }
      else
      {
        imageBuilder.Src = "Images/Security/" + str + "_allow_disabled.gif";
      }
      output.Write(imageBuilder.ToString());
      if (accessState == AccessState.Deny)
      {
        imageBuilder.Src = "Images/Security/" + str + "_deny_enabled.gif";
      }
      else
      {
        imageBuilder.Src = "Images/Security/" + str + "_deny_disabled.gif";
      }
      output.Write(imageBuilder.ToString());
    }
  }
}