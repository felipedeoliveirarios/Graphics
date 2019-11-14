using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class ContextData : JsonObject
    {
        [SerializeField]
        string m_DisplayName;

        [SerializeField]
        TypeRef<IContext> m_ContextType;

        [SerializeField]
        JsonList<BlockData> m_Blocks = new JsonList<BlockData>();

        [SerializeField]
        JsonList<PortData> m_InputPorts = new JsonList<PortData>();

        [SerializeField]
        JsonList<PortData> m_OutputPorts = new JsonList<PortData>();

        [SerializeField]
        Vector2 m_Position;

        public string displayName
        {
            get => m_DisplayName;
            set => m_DisplayName = value;
        }

        public TypeRef<IContext> contextType
        {
            get => m_ContextType;
            set => m_ContextType = value;
        }

        public List<BlockData> blocks => m_Blocks;

        public JsonList<PortData> inputPorts
        {
            get => m_InputPorts;
            set => m_InputPorts = value;
        }

        public JsonList<PortData> outputPorts
        {
            get => m_OutputPorts;
            set => m_OutputPorts = value;
        }

        public Vector2 position
        {
            get => m_Position;
            set => m_Position = value;
        }
    }
}
