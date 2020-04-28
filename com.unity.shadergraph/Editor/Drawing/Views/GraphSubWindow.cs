﻿using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using IResizable = UnityEditor.ShaderGraph.Drawing.IResizable;
using ResizableElement = UnityEditor.ShaderGraph.Drawing.ResizableElement;

namespace Drawing.Views
{
    public class GraphSubWindow : GraphElement, ISelection, IResizable
    {
        protected VisualElement m_MainContainer;
        protected VisualElement m_Root;
        protected Label m_TitleLabel;
        protected Label m_SubTitleLabel;
        protected ScrollView m_ScrollView;
        protected VisualElement m_ContentContainer;
        protected VisualElement m_HeaderItem;

        private bool m_Scrollable = false;

        private Dragger m_Dragger;
        protected GraphView m_GraphView;

        public WindowDockingLayout windowDockingLayout { get; private set; }

        // This needs to be something that each subclass defines on its own
        // if they all use the same they'll be stacked on top of each other at SG window creation
        WindowDockingLayout m_DefaultLayout =new WindowDockingLayout
        {
            dockingTop = true,
            dockingLeft = false,
            verticalOffset = 8,
            horizontalOffset = 8,
            size = new Vector2(300, 300),
        };

        private const string UxmlName = "GraphSubWindow";

        // These are used as default values for styling and layout purposes
        // They can be overriden if a child class wants to roll its own style and layout behavior
        protected virtual string layoutKey => "ShaderGraph.SubWindow";
        protected virtual string styleName => "GraphSubWindow";

        // Each sub-window will override these if they need to
        protected virtual string elementName => "";
        protected virtual string windowTitle => "";

        public GraphView graphView
        {
            get
            {
                if (!windowed && m_GraphView == null)
                    m_GraphView = GetFirstAncestorOfType<GraphView>();
                return m_GraphView;
            }

            set
            {
                if (!windowed)
                    return;
                m_GraphView = value;
            }
        }

        // ISelection implementation
        public List<ISelectable> selection
        {
            get
            {
                return graphView?.selection;
            }
        }

        public override string title
        {
            get { return m_TitleLabel.text; }
            set { m_TitleLabel.text = value; }
        }

        public string subTitle
        {
            get { return m_SubTitleLabel.text; }
            set { m_SubTitleLabel.text = value; }
        }

        bool m_Windowed;
        public bool windowed
        {
            get { return m_Windowed; }
            set
            {
                if (m_Windowed == value) return;

                if (value)
                {
                    capabilities &= ~Capabilities.Movable;
                    AddToClassList("windowed");
                    this.RemoveManipulator(m_Dragger);
                }
                else
                {
                    capabilities |= Capabilities.Movable;
                    RemoveFromClassList("windowed");
                    this.AddManipulator(m_Dragger);
                }
                m_Windowed = value;
            }
        }

        public override VisualElement contentContainer => m_ContentContainer;

        public bool scrollable
        {
            get
            {
                return m_Scrollable;
            }
            set
            {
                if (m_Scrollable == value)
                    return;

                m_Scrollable = value;

                if (m_Scrollable)
                {
                    if (m_ScrollView == null)
                    {
                        m_ScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
                    }

                    // Remove the sections container from the content item and add it to the scrollview
                    m_ContentContainer.RemoveFromHierarchy();
                    m_Root.Add(m_ScrollView);
                    m_ScrollView.Add(m_ContentContainer);

                    AddToClassList("scrollable");
                }
                else
                {
                    if (m_ScrollView != null)
                    {
                        // Remove the sections container from the scrollview and add it to the content item
                        m_ScrollView.RemoveFromHierarchy();
                        m_ContentContainer.RemoveFromHierarchy();
                        m_Root.Add(m_ContentContainer);
                    }
                    RemoveFromClassList("scrollable");
                }
            }
        }

        protected GraphSubWindow(GraphView associatedGraphView = null) : base()
        {
            m_GraphView = associatedGraphView;
            m_GraphView.Add(this);

            // Setup VisualElement from Stylesheet and UXML file
            styleSheets.Add(Resources.Load<StyleSheet>($"Styles/{styleName}"));
            var uxml = Resources.Load<VisualTreeAsset>($"UXML/{UxmlName}");
            m_MainContainer = uxml.Instantiate();
            m_MainContainer.AddToClassList("mainContainer");

            m_Root = m_MainContainer.Q("content");
            m_HeaderItem = m_MainContainer.Q("header");
            m_HeaderItem.AddToClassList("subWindowHeader");

            m_TitleLabel = m_MainContainer.Q<Label>(name: "titleLabel");
            m_SubTitleLabel = m_MainContainer.Q<Label>(name: "subTitleLabel");
            m_ContentContainer = m_MainContainer.Q(name: "contentContainer");

            hierarchy.Add(m_MainContainer);

            capabilities |= Capabilities.Movable | Capabilities.Resizable;
            style.overflow = Overflow.Hidden;
            focusable = false;
            scrollable = true;
            name = elementName;
            title = windowTitle;

            ClearClassList();
            AddToClassList(name);

            BuildManipulators();

            /* Event interception to prevent GraphView manipulators from being triggered */
            RegisterCallback<DragUpdatedEvent>(e =>
            {
                e.StopPropagation();
            });

            // prevent Zoomer manipulator
            RegisterCallback<WheelEvent>(e =>
            {
                e.StopPropagation();
            });

            RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == (int)MouseButton.LeftMouse)
                    ClearSelection();
                // prevent ContentDragger manipulator
                e.StopPropagation();
            });
        }

        public virtual void AddToSelection(ISelectable selectable)
        {
            graphView?.AddToSelection(selectable);
        }

        public virtual void RemoveFromSelection(ISelectable selectable)
        {
            graphView?.RemoveFromSelection(selectable);
        }

        public virtual void ClearSelection()
        {
            graphView?.ClearSelection();
        }

        void BuildManipulators()
        {
            m_Dragger = new Dragger { clampToParentEdges = true };
            this.RegisterCallback<MouseUpEvent>(OnMoved);
            this.AddManipulator(m_Dragger);

            var resizeElement = this.Q<ResizableElement>();
            resizeElement.BindOnResizeCallback(OnWindowResize);
            hierarchy.Add(resizeElement);
        }

        void OnMoved(MouseUpEvent upEvent)
        {
            windowDockingLayout.CalculateDockingCornerAndOffset(this.layout, graphView.layout);
            windowDockingLayout.ClampToParentWindow();

            SerializeLayout();
        }

        void OnWindowResize(MouseUpEvent upEvent)
        {
        }

        void SerializeLayout()
        {
            var serializedLayout = JsonUtility.ToJson(windowDockingLayout);
            EditorUserSettings.SetConfigValue(layoutKey, serializedLayout);
        }

        public void DeserializeLayout()
        {
            var serializedLayout = EditorUserSettings.GetConfigValue(layoutKey);
            if (!string.IsNullOrEmpty(serializedLayout))
                windowDockingLayout = JsonUtility.FromJson<WindowDockingLayout>(serializedLayout);
            else
                windowDockingLayout = m_DefaultLayout;

            windowDockingLayout.ApplySize(this);
            windowDockingLayout.ApplyPosition(this);
        }

        public void OnStartResize()
        {
        }

        public void OnResized()
        {
            windowDockingLayout.size = this.layout.size;
            SerializeLayout();
        }
    }
}
