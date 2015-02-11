﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text.RegularExpressions;

namespace TestPSSnapIn
{
    /* First a very simple and unsecure tree structure that can be used by the classes */
    public class TestTreeNode
    {
        public string Name { get; set; }
        public TestTreeNode Parent { get; private set; }
        public virtual Dictionary<string, TestTreeNode> Children { get; private set; }

        public TestTreeNode(TestTreeNode parent, string name)
        {
            Name = name;
            Parent = parent;
            Children = new Dictionary<string, TestTreeNode>();
        }

        public virtual TestTreeNode DetachedCopy(string newName)
        {
            var copy = new TestTreeNode(null, newName);
            foreach (var pair in Children)
            {
                copy.Children[pair.Key] = pair.Value;
            }
            return copy;
        }

        public virtual void AddChild(TestTreeNode node)
        {
            Children[node.Name] = node;
            node.Parent = this;
        }
    }
    public class TestTreeLeaf : TestTreeNode
    {
        public override Dictionary<string, TestTreeNode> Children { get { return null; } }
        public string Value { get; set; }

        public TestTreeLeaf(TestTreeNode parent, string name, string value) : base(parent, name)
        {
            Value = value;
        }

        public override TestTreeNode DetachedCopy(string newName)
        {
            return new TestTreeLeaf(null, newName, Value);
        }

        public override void AddChild(TestTreeNode node)
        {
            throw new InvalidOperationException("Cannot add child to leaf node");
        }
    }

    public class TestNavigationDrive : PSDriveInfo
    {
        public TestTreeNode Tree { get; private set; }
        public TestNavigationDrive(PSDriveInfo info) : base(info)
        {
            Tree = new TestTreeNode(null, "root");
        }
    }

    [CmdletProvider(TestNavigationProvider.ProviderName, ProviderCapabilities.None)]
    public class TestNavigationProvider : NavigationCmdletProvider
    {
        public const string ProviderName = "TestNavigationProvider";
        public const string DefaultDriveName = "TestNavigationItems";
        public const string DefaultDriveRoot = "/def/";
        public const string DefaultDrivePath = DefaultDriveName + ":/";
        public const string DefaultItemName = "defItem";
        public const string DefaultItemPath = DefaultDrivePath + DefaultItemName;
        public const string DefaultItemValue = "defValue";
        public const string DefaultNodeName = "defNode";
        public const string DefaultNodePath = DefaultDrivePath + DefaultNodeName + "/";

        private const string _pathSeparator = "/";

        #region navigation related

        protected override string GetChildName (string path)
        {
            string itemName;
            ParentPath(path, out itemName);
            return itemName;
        }

        protected override string GetParentPath(string path, string root)
        {
            // TODO: implement correctly with regard to root
            return ParentPath(path);
        }

        protected override bool IsItemContainer(string path)
        {
            var node = FindNode(path, false);
            return node != null && IsContainer(node);
        }

        protected override string MakePath(string parent, string child)
        {
            throw new NotImplementedException();
        }

        protected override void MoveItem(string path, string destination)
        {
            throw new NotImplementedException();
        }

        protected override string NormalizeRelativePath(string path, string basePath)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region container related

        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            var srcNode = FindNode(path);
            string newName;
            var destParent = FindParent(copyPath, out newName);
            // copy container without recursion: create empty container
            if (IsContainer(srcNode) && !recurse)
            {
                var copy = new TestTreeNode(destParent, srcNode.Name);
                destParent.AddChild(copy);
                WriteItemObject(copy, copyPath, true);
                return;
            }
            // create a full copy if destination doesn't exist or is a leaf and should get ovewritten
            if (!ItemExists(copyPath) || !IsContainer(FindNode(copyPath)))
            {
                var copiedNode = srcNode.DetachedCopy(newName);
                destParent.AddChild(copiedNode);
                WriteItemObject(copiedNode, copyPath, IsContainer(copiedNode));
                return;
            }
            // else dest is an existing container. copy into it
            var destNode = FindNode(copyPath);
            if (!IsContainer(srcNode))
            {
                var copy = srcNode.DetachedCopy(srcNode.Name);
                destNode.AddChild(copy);
                WriteItemObject(copy, copyPath + "/" + srcNode.Name, false);
                return;
            }
            // otherwise we copy the contents of src into dest
            foreach (var child in srcNode.Children.Values)
            {
                var copy = child.DetachedCopy(child.Name);
                destNode.AddChild(copy);
            }
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            if (!HasChildItems(path))
            {
                return;
            }
            var node = FindNode(path);
            if (path.EndsWith(_pathSeparator))
            {
                path = path.Substring(0, path.Length - _pathSeparator.Length);
            }
            foreach (var child in node.Children.Values)
            {
                var childPath = String.Join(_pathSeparator, new [] { path, child.Name });
                WriteItemObject(child, childPath, IsContainer(child));
                if (recurse)
                {
                    GetChildItems(childPath, true);
                }
            }
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            if (!HasChildItems(path))
            {
                return;
            }
            var node = FindNode(path);
            if (path.EndsWith(_pathSeparator))
            {
                path = path.Substring(0, path.Length - _pathSeparator.Length);
            }
            foreach (var child in node.Children.Values)
            {
                var childPath = String.Join(_pathSeparator, new [] { path, child.Name });
                WriteItemObject(child.Name, childPath, IsContainer(child));
            }
        }

        protected override bool HasChildItems(string path)
        {
            var node = FindNode(path, false);
            return node != null && node.Children != null && node.Children.Count > 0;
        }

        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            var strValue = newItemValue as string;
            var isLeaf = itemTypeName.Equals("leaf", StringComparison.OrdinalIgnoreCase);
            if (!isLeaf && !itemTypeName.Equals("node", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The item type must be either 'Leaf' or 'Node'");
            }
            if (isLeaf && strValue == null)
            {
                throw new ArgumentException("Value for new leaf is either null or not a string");
            }

            string itemName;
            var parent = FindParent(path, out itemName);
            if (parent.Children.ContainsKey(itemName))
            {
                throw new ArgumentException("Item with path '" + path + "' already exists.");
            }
            var newItem = isLeaf ? new TestTreeLeaf(parent, itemName, strValue) : new TestTreeNode(parent, itemName);
            parent.Children[itemName] = newItem;
            WriteItemObject(newItem, path, IsContainer(newItem));
        }
            

        protected override void RemoveItem(string path, bool recurse)
        {
            // !HasChildItem || Recurse check is done by PS/Pash itself
            var node = FindNode(path);
            node.Parent.Children.Remove(node.Name);
        }

        #endregion

        #region item related

        protected override bool IsValidPath(string path)
        {
            // path with '/' as delimiter and names consisting of alphanumeric chars. first and last delimiter are optional
            return Regex.IsMatch(path, "^/?([a-zA-Z0-9]+/)*([a-zA-Z0-9]*)$");
        }

        protected override void RenameItem(string path, string newName)
        {
            var node = FindNode(path);
            if (node.Parent.Children.ContainsKey(newName))
            {
                throw new ArgumentException("Item with name '" + newName + "' already exists");
            }
            node.Parent.Children.Remove(node.Name);
            node.Name = newName;
            node.Parent.Children[newName] = node;
            WriteItemObject(node, "", IsContainer(node));
        }

        protected override void GetItem(string path)
        {
            var node = FindNode(path);
            WriteItemObject(node, path, IsContainer(node));
        }

        protected override bool ItemExists(string path)
        {
            return FindNode(path, false) != null;
        }

        #endregion

        #region drive related

        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            if (drive is TestNavigationDrive)
            {
                return drive;
            }
            return new TestNavigationDrive(drive);
        }

        protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        {
            var defDrives = new Collection<PSDriveInfo>();
            var drive = new TestNavigationDrive(new PSDriveInfo(DefaultDriveName, ProviderInfo, DefaultDriveRoot,
                "Default drive for testing container items", null));

            var defItem = new TestTreeLeaf(drive.Tree, DefaultItemName, DefaultItemValue);
            drive.Tree.AddChild(defItem);

            var defNode = new TestTreeNode(drive.Tree, DefaultNodeName);
            drive.Tree.AddChild(defNode);

            defDrives.Add(drive);
            return defDrives;
        }

        #endregion

        private string ParentPath(string path)
        {
            string itemName;
            return ParentPath(path, out itemName);
        }

        private string ParentPath(string path, out string itemName)
        {
            if (path.EndsWith(_pathSeparator))
            {
                path = path.Substring(0, path.Length - _pathSeparator.Length);
            }
            var sepIdx = path.LastIndexOf(_pathSeparator);
            itemName = path.Substring(sepIdx + 1);
            return path = sepIdx < 0 ? "" : path.Substring(0, sepIdx);
        }

        private TestTreeNode FindParent(string path, out string itemName)
        {
            return FindNode(ParentPath(path, out itemName));
        }

        private TestTreeNode FindNode(string path, bool throwOnError = true)
        {
            var root = (PSDriveInfo as TestNavigationDrive).Tree;
            var relpath = PathWithoutDrive(path);
            if (String.IsNullOrEmpty(relpath))
            {
                return root;
            }
            var components = relpath.Split(new [] { _pathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            var curnode = root;
            // simply go through the tree
            foreach (var comp in components)
            {
                if (curnode.Children == null || !curnode.Children.ContainsKey(comp))
                {
                    if (!throwOnError)
                    {
                        return null;
                    }
                    throw new ItemNotFoundException("Item of path '" + path + "' doesn't exist");
                }
                curnode = curnode.Children[comp];
            }
            return curnode;
        }

        private bool IsContainer(TestTreeNode node)
        {
            return !(node is TestTreeLeaf);
        }

        private string PathWithoutDrive(string path)
        {
            if (path.StartsWith(PSDriveInfo.Root))
            {
                path = path.Substring(PSDriveInfo.Root.Length);
            }
            if (path.StartsWith(_pathSeparator))
            {
                path = path.Substring(_pathSeparator.Length);
            }
            return path;
        }
    }
}