using Godot;
using Godot.Collections;

public class NX : Node
{
    //<summary>
    //Find the first node of type T in children
    //</summary>
    public static T Find<T>(Node root, bool recursive = false) where T : Node
    {
        for(int i = 0;i < root.GetChildCount(); ++i)
        {
            Node child = root.GetChild(i);
            if (child is T) return (T)child;
            if (recursive && child.GetChildCount() > 0)
            {
                T grandChild = Find<T>(child, true);
                if (grandChild != null) return grandChild;
            }
        }
        return null;
    }
    //<summary>
    //Find all nodes of type T in children
    //</summary>
    public static Array<T> FindAll<T>(Node root, bool recursive) where T : Node
    {
        Array<T> nodes = new Array<T>();
        for (int i = 0; i < root.GetChildCount(); ++i)
        {
            Node child = root.GetChild(i);
            if (child is T) nodes.Add((T)child);
            if (recursive && child.GetChildCount() > 0)
            {
                Array<T> grandChildren = FindAll<T>(child, true);
                if (grandChildren.Count != 0)
                {
                    foreach(T grandChild in grandChildren)
                    {
                        nodes.Add(grandChild);
                    }
                }
            }
        }
        return nodes;
    }
    //<summary>
    //Find all nodes of type T in parents
    //</summary>
    public static T FindParent<T>(Node root) where T : Node
    {
        root = root.GetParentOrNull<Node>();
        while (root != null)
        {
            if (root is T) break;
            root = root.GetParentOrNull<Node>();
        }
        return (T)root;
    }

    public static Array<T> FindAllParent<T>(Node root) where T : Node
    {
        Array<T> nodes = new Array<T>();
        root = root.GetParentOrNull<Node>();
        while (root != null)
        {
            if (root is T) nodes.Add((T)root);
            root = root.GetParentOrNull<Node>();
        }
        return nodes;
    }
}


