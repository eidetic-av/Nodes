﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace XNode
{
    /// <summary> Precaches reflection data in editor so we won't have to do it runtime </summary>
    public static class NodeDataCache
    {
        private static PortDataCache portDataCache;
        private static bool Initialized { get { return portDataCache != null; } }

        /// <summary> Update static ports to reflect class fields. </summary>
        public static void UpdatePorts(Node node, Dictionary<string, NodePort> ports)
        {
            if (!Initialized) BuildCache();

            Dictionary<string, NodePort> staticPorts = new Dictionary<string, NodePort>();
            System.Type nodeType = node.GetType();

            List<NodePort> typePortCache;
            if (portDataCache.TryGetValue(nodeType, out typePortCache))
            {
                for (int i = 0; i < typePortCache.Count; i++)
                {
                    staticPorts.Add(typePortCache[i].MemberName, portDataCache[nodeType][i]);
                }
            }

            // Cleanup port dict - Remove nonexisting static ports - update static port types
            // Loop through current node ports
            foreach (NodePort port in ports.Values.ToList())
            {
                // If port still exists, check it it has been changed
                NodePort staticPort;
                if (staticPorts.TryGetValue(port.MemberName, out staticPort))
                {
                    // If port exists but with wrong settings, remove it. Re-add it later.
                    if (port.connectionType != staticPort.connectionType || port.IsDynamic || port.direction != staticPort.direction) ports.Remove(port.MemberName);
                    else port.ValueType = staticPort.ValueType;
                }
                // If port doesn't exist anymore, remove it
                else if (port.IsStatic) ports.Remove(port.MemberName);
            }
            // Add missing ports
            foreach (NodePort staticPort in staticPorts.Values)
            {
                if (!ports.ContainsKey(staticPort.MemberName))
                {
                    ports.Add(staticPort.MemberName, new NodePort(staticPort, node));
                }
            }
        }

        private static void BuildCache()
        {
            portDataCache = new PortDataCache();
            System.Type baseType = typeof(Node);
            List<System.Type> nodeTypes = new List<System.Type>();
            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            Assembly selfAssembly = Assembly.GetAssembly(baseType);
            if (selfAssembly.FullName.StartsWith("Assembly-CSharp") && !selfAssembly.FullName.Contains("-firstpass"))
            {
                // If xNode is not used as a DLL, check only CSharp (fast)
                nodeTypes.AddRange(selfAssembly.GetTypes().Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)));
            }
            else
            {
                // Else, check all relevant DDLs (slower)
                // ignore all unity related assemblies
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly.FullName.StartsWith("Unity")) continue;
                    // unity created assemblies always have version 0.0.0
                    if (!assembly.FullName.Contains("Version=0.0.0")) continue;
                    nodeTypes.AddRange(assembly.GetTypes().Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)).ToArray());
                }
            }
            for (int i = 0; i < nodeTypes.Count; i++)
            {
                CachePorts(nodeTypes[i]);
            }
        }

        private static void CachePorts(System.Type nodeType)
        {
            // Cache ports attributed to fields
            System.Reflection.MemberInfo[] fieldInfo = nodeType.GetFields();
            for (int i = 0; i < fieldInfo.Length; i++)
            {

                //Get InputAttribute and OutputAttribute
                object[] attributes = fieldInfo[i].GetCustomAttributes(false);
                Node.InputAttribute inputAttribute = attributes.FirstOrDefault(x => x is Node.InputAttribute) as Node.InputAttribute;
                Node.OutputAttribute outputAttribute = attributes.FirstOrDefault(x => x is Node.OutputAttribute) as Node.OutputAttribute;

                if (inputAttribute == null && outputAttribute == null) continue;

                if (inputAttribute != null && outputAttribute != null) UnityEngine.Debug.LogError("Field " + fieldInfo[i].Name + " of type " + nodeType.FullName + " cannot be both input and output.");
                else
                {
                    if (!portDataCache.ContainsKey(nodeType)) portDataCache.Add(nodeType, new List<NodePort>());
                    portDataCache[nodeType].Add(new NodePort(fieldInfo[i]));
                }
            }
            // Cache ports that are attributed to properties
            System.Reflection.PropertyInfo[] propertyInfo = nodeType.GetProperties();
            for (int i = 0; i < propertyInfo.Length; i++)
            {

                //Get InputPropertyAttribute and OutputAttribute
                object[] attributes = propertyInfo[i].GetCustomAttributes(false);
                var inputAttribute = attributes.FirstOrDefault(x => x is Node.InputAttribute) as Node.InputAttribute;
                var outputAttribute = attributes.FirstOrDefault(x => x is Node.OutputAttribute) as Node.OutputAttribute;

                if (inputAttribute == null && outputAttribute == null) continue;
                else
                {
                    if (!portDataCache.ContainsKey(nodeType)) portDataCache.Add(nodeType, new List<NodePort>());
                    portDataCache[nodeType].Add(new NodePort(propertyInfo[i]));
                }
            }
        }

        [System.Serializable]
        private class PortDataCache : Dictionary<System.Type, List<NodePort>>, ISerializationCallbackReceiver
        {
            [SerializeField] private List<System.Type> keys = new List<System.Type>();
            [SerializeField] private List<List<NodePort>> values = new List<List<NodePort>>();

            // save the dictionary to lists
            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();
                foreach (var pair in this)
                {
                    keys.Add(pair.Key);
                    values.Add(pair.Value);
                }
            }

            // load dictionary from lists
            public void OnAfterDeserialize()
            {
                this.Clear();

                if (keys.Count != values.Count)
                    throw new System.Exception(string.Format("there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

                for (int i = 0; i < keys.Count; i++)
                    this.Add(keys[i], values[i]);
            }
        }
    }
}