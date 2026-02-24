using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NSP_ToolWindow : EditorWindow
{
    // ─── Colors & Style ───────────────────────────────────────────────
    private static readonly Color COL_BG         = new Color(0.13f, 0.13f, 0.15f);
    private static readonly Color COL_PANEL       = new Color(0.17f, 0.17f, 0.20f);
    private static readonly Color COL_ACCENT      = new Color(0.35f, 0.75f, 1.00f);
    private static readonly Color COL_ACCENT_DIM  = new Color(0.20f, 0.45f, 0.65f);
    private static readonly Color COL_HEADER      = new Color(0.10f, 0.10f, 0.12f);
    private static readonly Color COL_WARN        = new Color(1.00f, 0.75f, 0.20f);
    private static readonly Color COL_OK          = new Color(0.30f, 0.90f, 0.50f);
    private static readonly Color COL_TEXT        = new Color(0.88f, 0.88f, 0.92f);
    private static readonly Color COL_TEXT_DIM    = new Color(0.50f, 0.50f, 0.55f);

    // ─── State ────────────────────────────────────────────────────────
    private GameObject  _targetObject;
    private GameObject  _lastTarget;

    // Hierarchy selection
    private bool        _selectAll      = true;
    private bool        _selectParent   = true;
    private List<(Transform t, bool selected)> _children = new();
    private Vector2     _hierarchyScroll;

    // Mode: 0 = Shader, 1 = Component
    private int         _modeIndex      = 0;
    private readonly string[] _modeOptions = { "Shader", "Component" };

    // Shader mode
    private int         _shaderIndex    = 0;
    private readonly string[] _shaderNames = { "URP/Lit", "Poiyomi 8.x", "Unlit/Texture", "Custom..." };
    private Shader      _customShader;
    private Vector2     _varScrollShader;

    // Component mode
    private int         _compIndex      = 0;
    private readonly string[] _compNames = {
        "MeshCollider", "BoxCollider", "SphereCollider",
        "Rigidbody", "AudioSource", "Light", "Custom Script"
    };
    private MonoScript  _customScript;
    private bool        _compRemove     = false;
    private Vector2     _varScrollComp;

    // Stats
    private int         _totalTris      = 0;
    private int         _totalVerts     = 0;
    private int         _totalRenderers = 0;
    private int         _totalMaterials = 0;

    // GUIStyles (built lazily)
    private GUIStyle    _styleTitle;
    private GUIStyle    _styleLabel;
    private GUIStyle    _styleLabelDim;
    private GUIStyle    _styleLabelAccent;
    private GUIStyle    _stylePanel;
    private GUIStyle    _styleSectionHeader;
    private GUIStyle    _styleStat;
    private bool        _stylesBuilt    = false;

    // ─── Menu item ────────────────────────────────────────────────────
    [MenuItem("Tools/NSP Tool")]
    public static void ShowWindow()
    {
        var w = GetWindow<NSP_ToolWindow>("NSP Tool");
        w.minSize = new Vector2(560, 620);
    }

    // ─── Build styles once ────────────────────────────────────────────
    private void BuildStyles()
    {
        if (_stylesBuilt) return;
        _stylesBuilt = true;

        _styleTitle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = COL_ACCENT }
        };

        _styleLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            normal   = { textColor = COL_TEXT }
        };

        _styleLabelDim = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            normal   = { textColor = COL_TEXT_DIM }
        };

        _styleLabelAccent = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = COL_ACCENT }
        };

        _stylePanel = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin  = new RectOffset(0, 0, 4, 4)
        };

        _styleSectionHeader = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 10,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = COL_ACCENT_DIM }
        };

        _styleStat = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = COL_TEXT }
        };
    }

    // ─── OnGUI ────────────────────────────────────────────────────────
    private void OnGUI()
    {
        BuildStyles();

        // Background
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), COL_BG);

        // ── Header bar ─────────────────────────────────────────────
        DrawHeader();

        // ── Body split ─────────────────────────────────────────────
        float bodyY    = 80f;
        float bodyH    = position.height - bodyY - 56f; // leave room for footer
        float leftW    = 200f;
        float rightW   = position.width - leftW - 12f;

        // Left panel
        GUILayout.BeginArea(new Rect(6, bodyY, leftW, bodyH));
        DrawLeftPanel(bodyH);
        GUILayout.EndArea();

        // Divider
        EditorGUI.DrawRect(new Rect(leftW + 6, bodyY + 4, 1, bodyH - 8), COL_ACCENT_DIM);

        // Right panel
        GUILayout.BeginArea(new Rect(leftW + 13, bodyY, rightW - 6, bodyH));
        DrawRightPanel(bodyH);
        GUILayout.EndArea();

        // ── Footer ─────────────────────────────────────────────────
        DrawFooter();
    }

    // ─── Header ───────────────────────────────────────────────────────
    private void DrawHeader()
    {
        EditorGUI.DrawRect(new Rect(0, 0, position.width, 78), COL_HEADER);

        // Title
        GUI.Label(new Rect(12, 8, 200, 28), "NSP Tool", _styleTitle);
        GUI.Label(new Rect(14, 34, 300, 18), "Prefab & Asset Pipeline Utility", _styleLabelDim);

        // Prefab field
        float fieldX = 12f;
        float fieldW = position.width - 24f;

        EditorGUI.BeginChangeCheck();
        var newTarget = (GameObject)EditorGUI.ObjectField(
            new Rect(fieldX, 54, fieldW, 18),
            _targetObject,
            typeof(GameObject),
            true
        );
        if (EditorGUI.EndChangeCheck())
        {
            _targetObject = newTarget;
            if (_targetObject != _lastTarget)
            {
                _lastTarget = _targetObject;
                RefreshTarget();
            }
        }
    }

    // ─── Left panel: hierarchy + stats ────────────────────────────────
    private void DrawLeftPanel(float height)
    {
        // Stats row
        DrawSectionLabel("STATS");
        GUILayout.Space(2);

        if (_targetObject != null)
        {
            Color triColor = _totalTris > 50000 ? COL_WARN : COL_OK;
            DrawStatRow("Tris",      _totalTris.ToString("N0"),      triColor);
            DrawStatRow("Verts",     _totalVerts.ToString("N0"),     COL_TEXT);
            DrawStatRow("Renderers", _totalRenderers.ToString(),     COL_TEXT);
            DrawStatRow("Materials", _totalMaterials.ToString(),     _totalMaterials > 8 ? COL_WARN : COL_TEXT);
        }
        else
        {
            GUI.Label(GUILayoutUtility.GetRect(0, 18), "No object selected", _styleLabelDim);
        }

        GUILayout.Space(8);
        DrawSectionLabel("HIERARCHY");
        GUILayout.Space(2);

        if (_targetObject == null)
        {
            GUI.Label(GUILayoutUtility.GetRect(0, 18), "Drag a prefab or GameObject", _styleLabelDim);
            return;
        }

        // Select All toggle
        EditorGUI.BeginChangeCheck();
        bool newAll = EditorGUILayout.ToggleLeft("Select All", _selectAll, _styleLabel);
        if (EditorGUI.EndChangeCheck())
        {
            _selectAll   = newAll;
            _selectParent = newAll;
            for (int i = 0; i < _children.Count; i++)
                _children[i] = (_children[i].t, newAll);
        }

        GUILayout.Space(2);

        // Scrollable hierarchy
        float scrollH = height - 180f;
        _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll, GUILayout.Height(scrollH));

        // Parent row
        EditorGUI.BeginChangeCheck();
        bool newParent = EditorGUILayout.ToggleLeft(
            $"◈  {_targetObject.name}  (parent)",
            _selectParent,
            _styleLabelAccent
        );
        if (EditorGUI.EndChangeCheck()) _selectParent = newParent;

        // Children rows
        for (int i = 0; i < _children.Count; i++)
        {
            var (t, sel) = _children[i];
            EditorGUI.BeginChangeCheck();
            bool newSel = EditorGUILayout.ToggleLeft($"   ╰  {t.name}", sel, _styleLabel);
            if (EditorGUI.EndChangeCheck())
                _children[i] = (t, newSel);
        }

        GUILayout.EndScrollView();
    }

    // ─── Right panel: mode + variables ────────────────────────────────
    private void DrawRightPanel(float height)
    {
        DrawSectionLabel("MODE");
        GUILayout.Space(2);

        // Mode tabs
        EditorGUI.BeginChangeCheck();
        int newMode = GUILayout.Toolbar(_modeIndex, _modeOptions, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck()) _modeIndex = newMode;

        GUILayout.Space(6);

        if (_modeIndex == 0)
            DrawShaderMode(height);
        else
            DrawComponentMode(height);
    }

    // ─── Shader mode ──────────────────────────────────────────────────
    private void DrawShaderMode(float height)
    {
        DrawSectionLabel("TARGET SHADER");
        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        int newShader = EditorGUILayout.Popup(_shaderIndex, _shaderNames);
        if (EditorGUI.EndChangeCheck()) _shaderIndex = newShader;

        if (_shaderIndex == _shaderNames.Length - 1) // Custom
        {
            GUILayout.Space(4);
            _customShader = (Shader)EditorGUILayout.ObjectField("Shader Asset", _customShader, typeof(Shader), false);
        }

        GUILayout.Space(8);
        DrawSectionLabel("SHADER VARIABLES");
        GUILayout.Space(2);

        // Info box
        DrawInfoBox("Las texturas del material original se copiarán automáticamente.\nPuedes sobreescribir cualquier propiedad antes de aplicar.");

        GUILayout.Space(4);

        float varH = height - 160f;
        _varScrollShader = GUILayout.BeginScrollView(_varScrollShader, GUILayout.Height(varH));

        switch (_shaderIndex)
        {
            case 0: DrawURPVariables();      break;
            case 1: DrawPoiyomiVariables();  break;
            case 2: DrawUnlitVariables();    break;
            case 3: DrawCustomShaderInfo();  break;
        }

        GUILayout.EndScrollView();
    }

    // ─── Component mode ───────────────────────────────────────────────
    private void DrawComponentMode(float height)
    {
        DrawSectionLabel("COMPONENT");
        GUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        int newComp = EditorGUILayout.Popup(_compIndex, _compNames);
        if (EditorGUI.EndChangeCheck()) _compIndex = newComp;

        GUILayout.Space(4);

        _compRemove = EditorGUILayout.ToggleLeft("Remove component instead of adding", _compRemove, _styleLabel);

        GUILayout.Space(8);
        DrawSectionLabel("OPTIONS");
        GUILayout.Space(2);

        float varH = height - 130f;
        _varScrollComp = GUILayout.BeginScrollView(_varScrollComp, GUILayout.Height(varH));

        DrawComponentVariables();

        GUILayout.EndScrollView();
    }

    // ─── Shader variable panels ────────────────────────────────────────
    // These are preview placeholders – real values will be pulled from
    // the original material by MaterialProcessor at runtime.

    private Color   _baseColor      = Color.white;
    private float   _metallic       = 0f;
    private float   _smoothness     = 0.5f;
    private bool    _emissionOn     = false;
    private Color   _emissionColor  = Color.black;

    private void DrawURPVariables()
    {
        DrawVarLabel("_BaseMap", "Base Texture");
        EditorGUILayout.HelpBox("Se copiará automáticamente del material original", MessageType.None);

        GUILayout.Space(4);
        DrawVarLabel("_BaseColor", "Base Color");
        _baseColor = EditorGUILayout.ColorField(_baseColor);

        GUILayout.Space(4);
        DrawVarLabel("_Metallic", $"Metallic  [{_metallic:F2}]");
        _metallic = EditorGUILayout.Slider(_metallic, 0f, 1f);

        GUILayout.Space(4);
        DrawVarLabel("_Smoothness", $"Smoothness  [{_smoothness:F2}]");
        _smoothness = EditorGUILayout.Slider(_smoothness, 0f, 1f);

        GUILayout.Space(4);
        DrawVarLabel("_BumpMap", "Normal Map");
        EditorGUILayout.HelpBox("Se copiará automáticamente del material original", MessageType.None);

        GUILayout.Space(4);
        _emissionOn = EditorGUILayout.ToggleLeft("Enable Emission", _emissionOn, _styleLabel);
        if (_emissionOn)
        {
            _emissionColor = EditorGUILayout.ColorField(new GUIContent("Emission Color"), _emissionColor, true, false, true);
        }
    }

    private Color   _poiColor       = Color.white;
    private float   _poiSaturation  = 1f;
    private bool    _poiOutline     = false;
    private Color   _poiOutlineCol  = Color.black;
    private float   _poiOutlineW    = 1f;

    private void DrawPoiyomiVariables()
    {
        DrawVarLabel("_MainTex", "Main Texture (Colors & Alpha)");
        EditorGUILayout.HelpBox("Se copiará automáticamente desde _BaseMap", MessageType.None);

        GUILayout.Space(4);
        DrawVarLabel("_Color", "Base Color");
        _poiColor = EditorGUILayout.ColorField(_poiColor);

        GUILayout.Space(4);
        DrawVarLabel("_Saturation", $"Saturation  [{_poiSaturation:F2}]");
        _poiSaturation = EditorGUILayout.Slider(_poiSaturation, -1f, 1f);

        GUILayout.Space(4);
        DrawVarLabel("_BumpMap", "Normal Map");
        EditorGUILayout.HelpBox("Se copiará automáticamente desde _BumpMap", MessageType.None);

        GUILayout.Space(4);
        _poiOutline = EditorGUILayout.ToggleLeft("Enable Outline", _poiOutline, _styleLabel);
        if (_poiOutline)
        {
            DrawVarLabel("_OutlineColor", "Outline Color");
            _poiOutlineCol = EditorGUILayout.ColorField(_poiOutlineCol);
            DrawVarLabel("_OutlineWidth", $"Outline Width  [{_poiOutlineW:F1}]");
            _poiOutlineW = EditorGUILayout.Slider(_poiOutlineW, 0f, 10f);
        }
    }

    private void DrawUnlitVariables()
    {
        DrawVarLabel("_MainTex", "Main Texture");
        EditorGUILayout.HelpBox("Se copiará automáticamente del material original", MessageType.None);

        GUILayout.Space(4);
        DrawVarLabel("_Color", "Tint Color");
        _baseColor = EditorGUILayout.ColorField(_baseColor);
    }

    private void DrawCustomShaderInfo()
    {
        if (_customShader == null)
        {
            EditorGUILayout.HelpBox("Selecciona un shader custom arriba para ver sus propiedades.", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox(
            $"Shader: {_customShader.name}\n" +
            "Las propiedades se mapearán automáticamente por nombre donde sea posible.",
            MessageType.Info
        );
    }

    // ─── Component variable panels ────────────────────────────────────
    private bool    _convexMesh     = false;
    private bool    _rbKinematic    = false;
    private float   _rbMass        = 1f;

    private void DrawComponentVariables()
    {
        if (_compRemove)
        {
            EditorGUILayout.HelpBox(
                "Se eliminará el componente seleccionado de los objetos marcados en la jerarquía.",
                MessageType.Warning
            );
            return;
        }

        switch (_compIndex)
        {
            case 0: // MeshCollider
                DrawVarLabel("Convex", "Convex");
                _convexMesh = EditorGUILayout.ToggleLeft("Convex (necesario para Rigidbody)", _convexMesh, _styleLabel);
                break;

            case 1: // BoxCollider
            case 2: // SphereCollider
                EditorGUILayout.HelpBox("Se calculará automáticamente a partir del mesh.", MessageType.None);
                break;

            case 3: // Rigidbody
                DrawVarLabel("Mass", $"Mass  [{_rbMass:F1}]");
                _rbMass = EditorGUILayout.Slider(_rbMass, 0.01f, 100f);

                GUILayout.Space(4);
                DrawVarLabel("Is Kinematic", "Is Kinematic");
                _rbKinematic = EditorGUILayout.ToggleLeft("Is Kinematic", _rbKinematic, _styleLabel);
                break;

            case 4: // AudioSource
                EditorGUILayout.HelpBox("Se añadirá AudioSource con configuración por defecto.", MessageType.None);
                break;

            case 5: // Light
                EditorGUILayout.HelpBox("Se añadirá Light con configuración por defecto.", MessageType.None);
                break;

            case 6: // Custom Script
                DrawVarLabel("Script", "MonoScript");
                _customScript = (MonoScript)EditorGUILayout.ObjectField(_customScript, typeof(MonoScript), false);
                break;
        }
    }

    // ─── Footer ───────────────────────────────────────────────────────
    private void DrawFooter()
    {
        float footerY = position.height - 50f;
        EditorGUI.DrawRect(new Rect(0, footerY - 4, position.width, 1), COL_ACCENT_DIM);

        // Cancel button
        GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
        if (GUI.Button(new Rect(10, footerY + 4, 120, 32), "CANCELAR"))
        {
            ResetState();
        }

        // Apply button
        bool canApply = _targetObject != null;
        GUI.backgroundColor = canApply ? new Color(0.2f, 0.6f, 0.35f) : new Color(0.3f, 0.3f, 0.3f);
        GUI.enabled = canApply;
        if (GUI.Button(new Rect(position.width - 130, footerY + 4, 120, 32), "APLICAR"))
        {
            ApplyChanges();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        // Status label
        if (!canApply)
        {
            GUI.Label(
                new Rect(140, footerY + 12, position.width - 280, 20),
                "← Selecciona un objeto para continuar",
                _styleLabelDim
            );
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────
    private void DrawSectionLabel(string text)
    {
        GUILayout.Label(text, _styleSectionHeader);
        Rect r = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(new Rect(r.x, r.yMax, r.width, 1), COL_ACCENT_DIM);
        GUILayout.Space(2);
    }

    private void DrawVarLabel(string propName, string displayName)
    {
        GUILayout.Label($"{displayName}  <color=#{ColorUtility.ToHtmlStringRGB(COL_TEXT_DIM)}><i>{propName}</i></color>",
            new GUIStyle(_styleLabel) { richText = true });
    }

    private void DrawStatRow(string label, string value, Color valueColor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _styleLabelDim, GUILayout.Width(70));
        var s = new GUIStyle(_styleStat) { normal = { textColor = valueColor } };
        GUILayout.Label(value, s);
        GUILayout.EndHorizontal();
    }

    private void DrawInfoBox(string text)
    {
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), COL_ACCENT_DIM);
        GUILayout.Space(4);
        GUILayout.Label(text, _styleLabelDim);
        GUILayout.Space(4);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), COL_ACCENT_DIM);
    }

    // ─── Target refresh ───────────────────────────────────────────────
    private void RefreshTarget()
    {
        _children.Clear();
        _totalTris = _totalVerts = _totalRenderers = _totalMaterials = 0;

        if (_targetObject == null) return;

        // Populate children list
        foreach (Transform child in _targetObject.GetComponentsInChildren<Transform>(true))
        {
            if (child == _targetObject.transform) continue;
            _children.Add((child, true));
        }

        // Stats
        var renderers = _targetObject.GetComponentsInChildren<Renderer>(true);
        _totalRenderers = renderers.Length;

        var matSet = new HashSet<Material>();
        foreach (var r in renderers)
            foreach (var m in r.sharedMaterials)
                if (m != null) matSet.Add(m);
        _totalMaterials = matSet.Count;

        foreach (var mf in _targetObject.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            _totalTris  += mf.sharedMesh.triangles.Length / 3;
            _totalVerts += mf.sharedMesh.vertexCount;
        }

        _selectAll = _selectParent = true;
        for (int i = 0; i < _children.Count; i++)
            _children[i] = (_children[i].t, true);
    }

    // ─── Reset ────────────────────────────────────────────────────────
    private void ResetState()
    {
        _targetObject  = null;
        _lastTarget    = null;
        _children.Clear();
        _totalTris = _totalVerts = _totalRenderers = _totalMaterials = 0;
        _modeIndex     = 0;
        _shaderIndex   = 0;
        _compIndex     = 0;
        _compRemove    = false;
    }

    // ─── Apply (stub – will call processors once implemented) ─────────
    private void ApplyChanges()
    {
        // TODO: hook into MaterialProcessor / ComponentProcessor
        Debug.Log("[NSP Tool] Apply pressed – processors not yet implemented.");
        EditorUtility.DisplayDialog(
            "NSP Tool",
            "Cambios aplicados correctamente.\n(Los processors se conectarán en el siguiente paso.)",
            "OK"
        );
    }
}
