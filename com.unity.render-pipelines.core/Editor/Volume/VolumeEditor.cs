using NUnit.Framework;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering
{
    [CustomEditor(typeof(Volume))]
    sealed class VolumeEditor : Editor
    {
        static class Styles
        {
            public static readonly GUIContent labelProfile = EditorGUIUtility.TrTextContent("Profile", "A reference to a profile asset.");
            public static readonly GUIContent labelProfileInstance = EditorGUIUtility.TrTextContent("Profile (Instance)", "A copy of a profile asset.");
            public static readonly GUIContent iconNew = EditorGUIUtility.TrIconContent("CreateAddNew", "Create a new profile.");
            public static readonly GUIContent iconSaveAs = EditorGUIUtility.TrIconContent("SaveAs", "Save the instantiated profile");
            public static readonly GUIContent iconClone = EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Create a new profile and copy the content of the currently assigned profile.");
            public static readonly GUIContent iconCheckout = EditorGUIUtility.TrIconContent("editicon.sml", "Checkout the profile to enable edition.");
        }

        SerializedProperty m_IsGlobal;
        SerializedProperty m_BlendRadius;
        SerializedProperty m_Weight;
        SerializedProperty m_Priority;
        SerializedProperty m_Profile;

        VolumeComponentListEditor m_ComponentList;

        Volume actualTarget => target as Volume;

        VolumeProfile profileRef => actualTarget.HasInstantiatedProfile() ? actualTarget.profile : actualTarget.sharedProfile;

        readonly GUIContent[] m_Modes = { new GUIContent("Global"), new GUIContent("Local") };

        void OnEnable()
        {
            var o = new PropertyFetcher<Volume>(serializedObject);
            m_IsGlobal = o.Find(x => x.isGlobal);
            m_BlendRadius = o.Find(x => x.blendDistance);
            m_Weight = o.Find(x => x.weight);
            m_Priority = o.Find(x => x.priority);
            m_Profile = o.Find(x => x.sharedProfile);

            m_ComponentList = new VolumeComponentListEditor(this);
            RefreshEffectListEditor(actualTarget.sharedProfile);
        }

        void OnDisable()
        {
            m_ComponentList?.Clear();
        }

        void RefreshEffectListEditor(VolumeProfile asset)
        {
            m_ComponentList.Clear();

            if (asset != null)
                m_ComponentList.Init(asset, new SerializedObject(asset));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIContent label = EditorGUIUtility.TrTextContent("Mode", "A global volume is applied to the whole scene.");
            Rect lineRect = EditorGUILayout.GetControlRect();
            int isGlobal = m_IsGlobal.boolValue ? 0 : 1;
            EditorGUI.BeginProperty(lineRect, label, m_IsGlobal);
            {
                EditorGUI.BeginChangeCheck();
                isGlobal = EditorGUILayout.Popup(label, isGlobal, m_Modes);
                if (EditorGUI.EndChangeCheck())
                    m_IsGlobal.boolValue = isGlobal == 0;
            }
            EditorGUI.EndProperty();

            if (isGlobal != 0) // Blend radius is not needed for global volumes
            {
                if (!actualTarget.TryGetComponent<Collider>(out _))
                {
                    EditorGUILayout.HelpBox("Add a Collider to this GameObject to set boundaries for the local Volume.", MessageType.Info);

                    if (GUILayout.Button(EditorGUIUtility.TrTextContent("Add Collider"), EditorStyles.miniButton))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(EditorGUIUtility.TrTextContent("Box"), false, () => Undo.AddComponent<BoxCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Sphere"), false, () => Undo.AddComponent<SphereCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Capsule"), false, () => Undo.AddComponent<CapsuleCollider>(actualTarget.gameObject));
                        menu.AddItem(EditorGUIUtility.TrTextContent("Mesh"), false, () => Undo.AddComponent<MeshCollider>(actualTarget.gameObject));
                        menu.ShowAsContext();
                    }
                }

                EditorGUILayout.PropertyField(m_BlendRadius);
                m_BlendRadius.floatValue = Mathf.Max(m_BlendRadius.floatValue, 0f);
            }

            EditorGUILayout.PropertyField(m_Weight);
            EditorGUILayout.PropertyField(m_Priority);

            var assetHasChanged = false;
            var assetIsNotNull = m_Profile.objectReferenceValue != null && !m_Profile.objectReferenceValue.Equals(null);
            var assetIsMultiEdit = m_Profile.hasMultipleDifferentValues;
            var enableSaveOrClone = assetIsNotNull;
            var enableCheckout = VersionControl.Provider.isActive
                && !assetIsMultiEdit
                && assetIsNotNull
                && !AssetDatabase.IsOpenForEdit(m_Profile.objectReferenceValue, StatusQueryOptions.UseCachedIfPossible);

            // The layout system breaks alignment when mixing inspector fields with custom layout'd
            // fields, do the layout manually instead

            // Compute toolbar width and button styles
            const int k_ButtonIconWidth = 30;
            var toolbarWidth = k_ButtonIconWidth * 3;
            var buttonNewStyle = EditorStyles.miniButton;
            var buttonSaveOrCloneStyle = EditorStyles.miniButtonMid;
            var buttonCheckoutStyle = EditorStyles.miniButtonRight;

            // Compute the rect of each button
            var indentOffset = EditorGUI.indentLevel * 15f;
            lineRect = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(lineRect.x, lineRect.y, EditorGUIUtility.labelWidth - indentOffset, lineRect.height);
            var fieldRect = new Rect(labelRect.xMax, lineRect.y, lineRect.width - labelRect.width - toolbarWidth, lineRect.height);
            var buttonNewRect = new Rect(fieldRect.xMax, lineRect.y, k_ButtonIconWidth, lineRect.height);
            var buttonSaveOrCloneRect = new Rect(buttonNewRect.xMax, lineRect.y, k_ButtonIconWidth, lineRect.height);
            var buttonCheckOutRect = new Rect(buttonSaveOrCloneRect.xMax, lineRect.y, k_ButtonIconWidth, lineRect.height);

            // Draw the label
            var guiContent = actualTarget.HasInstantiatedProfile() ? Styles.labelProfileInstance : Styles.labelProfile;
            EditorGUI.PrefixLabel(labelRect, guiContent);

            // Draw the field
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUI.BeginProperty(fieldRect, GUIContent.none, m_Profile);

                VolumeProfile profile;

                if (actualTarget.HasInstantiatedProfile())
                    profile = (VolumeProfile)EditorGUI.ObjectField(fieldRect, actualTarget.profile, typeof(VolumeProfile), false);
                else
                    profile = (VolumeProfile)EditorGUI.ObjectField(fieldRect, m_Profile.objectReferenceValue, typeof(VolumeProfile), false);

                if (scope.changed)
                {
                    assetHasChanged = true;
                    m_Profile.objectReferenceValue = profile;

                    if (actualTarget.HasInstantiatedProfile()) // Clear the instantiated profile, from now on we're using shared again
                        actualTarget.profile = null;
                }

                EditorGUI.EndProperty();
            }

            // Draw the toolbar
            using (new EditorGUI.DisabledScope(assetIsMultiEdit))
            {
                if (GUI.Button(buttonNewRect, Styles.iconNew, buttonNewStyle))
                {
                    // By default, try to put assets in a folder next to the currently active
                    // scene file. If the user isn't a scene, put them in root instead.
                    var targetName = actualTarget.name;
                    var scene = actualTarget.gameObject.scene;
                    var asset = VolumeProfileFactory.CreateVolumeProfile(scene, targetName);
                    m_Profile.objectReferenceValue = asset;
                    actualTarget.profile = null; // Make sure we're not using an instantiated profile anymore
                    assetHasChanged = true;
                }

                guiContent = actualTarget.HasInstantiatedProfile() ? Styles.iconSaveAs : Styles.iconClone;
                using (new EditorGUI.DisabledScope(!enableSaveOrClone))
                {
                    if (GUI.Button(buttonSaveOrCloneRect, guiContent, buttonSaveOrCloneStyle))
                    {
                        // Duplicate the currently assigned profile and save it as a new profile
                        var origin = profileRef;
                        var path = AssetDatabase.GetAssetPath(m_Profile.objectReferenceValue);
                        path = AssetDatabase.GenerateUniqueAssetPath(path);

                        var asset = Instantiate(origin);
                        asset.components.Clear();
                        AssetDatabase.CreateAsset(asset, path);

                        foreach (var item in origin.components)
                        {
                            var itemCopy = Instantiate(item);
                            itemCopy.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                            itemCopy.name = item.name;
                            asset.components.Add(itemCopy);
                            AssetDatabase.AddObjectToAsset(itemCopy, asset);
                        }

                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        m_Profile.objectReferenceValue = asset;
                        actualTarget.profile = null; // Make sure we're not using an instantiated profile anymore
                        assetHasChanged = true;
                    }
                }

                guiContent = Styles.iconCheckout;
                using (new EditorGUI.DisabledScope(!enableCheckout))
                {
                    if (GUI.Button(buttonCheckOutRect, guiContent, buttonCheckoutStyle))
                    {
                        Assert.True(Provider.isActive);
                        Provider.Checkout(m_Profile.objectReferenceValue, CheckoutMode.Both);
                    }
                }
            }

            EditorGUILayout.Space();

            if (m_Profile.objectReferenceValue == null && !actualTarget.HasInstantiatedProfile())
            {
                if (assetHasChanged)
                    m_ComponentList.Clear(); // Asset wasn't null before, do some cleanup
            }
            else
            {
                if (assetHasChanged || profileRef != m_ComponentList.asset)
                {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    RefreshEffectListEditor(profileRef);
                }

                if (!assetIsMultiEdit)
                {
                    m_ComponentList.OnGUI();
                    EditorGUILayout.Space();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
