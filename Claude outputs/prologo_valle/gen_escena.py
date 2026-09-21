# -*- coding: utf-8 -*-
"""Genera Assets/Scenes/Worlds/Prologo_Valle.unity entera desde layout.py.

Se escribe el YAML a mano, sin el Editor, por la misma razón que la vez anterior: la sesión de
Cowork y Raúl se pelean por el foco de Play/Stop si se construye en vivo. La ventaja añadida es que
el decorado queda descrito en un fichero legible: mover el pueblo dos metros es cambiar un número y
volver a ejecutar esto, no arrastrar ciento treinta objetos.
"""
import os, sys, shutil, datetime
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unitylib as u
import layout as L

DESTINO = os.path.join(u.RAIZ, "Assets/Scenes/Worlds/Prologo_Valle.unity")

# fileIDs de los tres actores dentro de sus prefabs (ver tabla del documento del 17 sep)
ACTORES = [
    # (nombre, guid del prefab, Transform raíz, NPCBehaviourManagerV2, GameObject raíz, marca inicial)
    ("NPC_Archimago",  "0697f276b63523d4cad83528e85e3df2", 6581133412450051876, 981712127504577662, 8920793767228678463, "M_Apertura"),
    ("NPC_Liora",      "8fa16dcdaf0691c4f85b29439cf26073",  938401375732201111, 1378969436345142649, 8726245353716293871, "M_Mesa_Liora"),
    ("NPC_MagoOscuro", "0c3083176c02e614e8880d526d2db878", 9132289899736596217, 1408085124568394779, 8330914016721479921, "M_Duelo_Oscuro"),
]

# objetos del decorado que la secuencia puede encuadrar (id de actor -> nombre del objeto, altura de mira)
PROPS = [
    ("PROP_Horno",      "Horno_Mostrador", 1.10),
    ("PROP_Carreta",    "Carreta",         0.90),
    ("PROP_Globo",      "Globo",           1.20),
    ("PROP_Campanario", "Iglesia",         7.00),
    ("PROP_Mesa",       "Mesa",            0.95),
    ("PROP_Viga",       "Viga_Punto_De_Mira", 0.0),
    ("PROP_Incendio",   "PROP_Incendio",   2.50),
]

def generar():
    e = u.Escena()
    ids_marcas = {}
    props_tr = {}

    # ── 1. ajustes de escena ─────────────────────────────────────────────────
    # OJO: estos ajustes NO se aplican en partida. La escena se carga en aditivo y Unity coge el
    # cielo, la niebla y la luz ambiente de la escena ACTIVA (MainWorld). En partida manda
    # DayNightCycle, al que la secuencia le pide la hora del día con TimeOfDayBeat. Esto de aquí es
    # para poder abrir la escena suelta en el Editor y que se vea decente mientras se compone.
    cabecera = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 1
  m_FogColor: {r: 0.74, g: 0.78, b: 0.72, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.012
  m_LinearFogStart: 30
  m_LinearFogEnd: 140
  m_AmbientSkyColor: {r: 0.52, g: 0.56, b: 0.6, a: 1}
  m_AmbientEquatorColor: {r: 0.44, g: 0.45, b: 0.42, a: 1}
  m_AmbientGroundColor: {r: 0.25, g: 0.24, b: 0.2, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 2100000, guid: %s, type: 2}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_BakeOnSceneLoad: 0
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 2
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 1
    m_PVRFilteringGaussRadiusAO: 1
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}""" % u.guid("SkyBrightMorning.mat")
    e.añadir(cabecera)

    # ── 2. suelo ──────────────────────────────────────────────────────────────
    e.añadir(plano_suelo(e.bloque(5)))
    e.añadir(lamina_de_agua(e.bloque(4)))

    # ── 3. luz de edición (se apaga sola en partida) ──────────────────────────
    e.añadir(luz_direccional(e.bloque(4)))

    # ── 4. decorado ───────────────────────────────────────────────────────────
    raiz_go, raiz_tr = e.reservar()
    grupos = {}
    for g, prefab, nombre, x, y, z, rot, esc in L.D:
        grupos.setdefault(g, [])
    hijos_raiz = []
    grupos.pop("08_Incendio", None)
    for g in sorted(grupos):
        g_go, g_tr = e.reservar()
        hijos = []
        for grupo, prefab, nombre, x, y, z, rot, esc in L.D:
            if grupo != g or grupo == "08_Incendio": continue
            tr = e.prefab(prefab, nombre, (x, y, z), rot, esc, padre=g_tr)
            hijos.append(tr)
            for pid, obj, _alt in PROPS:
                if obj == nombre: props_tr[pid] = tr
        e.vacio(g, padre=raiz_tr, hijos=hijos, ids=(g_go, g_tr))
        hijos_raiz.append(g_tr)

    # el incendio: grupo aparte y APAGADO al empezar; lo enciende un beat en la fase 3
    f_go, f_tr = e.reservar()
    hijos_fuego = []
    # lo que solo existe cuando el valle arde (la viga caída y sus escombros)
    for grupo, prefab, nombre, x, y, z, rot, esc in L.D:
        if grupo != "08_Incendio": continue
        tr = e.prefab(prefab, nombre, (x, y, z), rot, esc, padre=f_tr)
        hijos_fuego.append(tr)
        for pid, obj, _alt in PROPS:
            if obj == nombre: props_tr[pid] = tr
    for nombre, x, z, esc in L.FUEGOS:
        hijos_fuego.append(e.prefab("Fire01_P.prefab", nombre, (x, L.S + 0.2, z), 0, esc, padre=f_tr, estatico=False))
        ids = e.bloque(3)
        e.añadir(luz_de_fuego(ids, nombre + "_Luz", (x, L.S + 1.4, z), f_tr))
        hijos_fuego.append(ids[1])
    for nombre, x, z, esc in L.HUMOS:
        hijos_fuego.append(e.prefab("Smoke02_P.prefab", nombre, (x, L.S + 3.0, z), 0, esc, padre=f_tr, estatico=False))
    # Punto al que apuntan los planos de la viga: su extremo BAJO, encima de la marca del
    # Archimago. Si se apunta al prefab, el plano encuadra el alero del que cuelga, que es lo que
    # menos importa de toda la escena.
    mira_go, mira_tr = e.reservar()
    e.vacio("Viga_Punto_De_Mira", pos=L.MIRA_VIGA, padre=f_tr, ids=(mira_go, mira_tr))
    hijos_fuego.append(mira_tr)
    props_tr["PROP_Viga"] = mira_tr

    e.vacio("PROP_Incendio", padre=raiz_tr, hijos=hijos_fuego, ids=(f_go, f_tr), activo=False)
    props_tr["PROP_Incendio"] = f_tr
    hijos_raiz.append(f_tr)

    e.vacio("DECORADO", padre=0, hijos=hijos_raiz, ids=(raiz_go, raiz_tr))

    # ── 5. actores ────────────────────────────────────────────────────────────
    marcas = {m[0]: m for m in L.MARCAS}
    for nombre, g, tr_src, comp_src, go_src, marca in ACTORES:
        _, x, z, rot = marcas[marca]
        e.añadir(instancia_actor(e, nombre, g, tr_src, comp_src, go_src, (x, L.S, z), rot))

    # ── 6. el objeto de la secuencia, con sus marcas ──────────────────────────
    seq_ids = e.bloque(6)
    seq_go, seq_tr = seq_ids[0], seq_ids[1]
    hijos_marcas = []
    for nombre, x, z, rot in L.MARCAS:
        m_go, m_tr = e.reservar()
        e.vacio(nombre, pos=(x, L.S, z), padre=seq_tr, rot_y=rot, ids=(m_go, m_tr))
        ids_marcas[nombre] = m_tr
        hijos_marcas.append(m_tr)
    e.añadir(objeto_secuencia(seq_ids, hijos_marcas, ids_marcas, props_tr))

    return "".join(e.docs)


def plano_suelo(ids):
    go, tr, mf, mr, mc = ids
    """El prado. Un solo plano grande: lo que da relieve son las lomas, no el suelo."""
    return """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: Suelo_Valle
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 4294967295
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 6000, y: 100, z: 6000}
  m_LocalScale: {x: 26, y: 1, z: 26}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &%d
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
--- !u!23 &%d
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: %s, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {fileID: 0}
--- !u!64 &%d
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 5
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}""" % (
        go, tr, mf, mr, mc, tr, go, mf, go, mr, go,
        u.guid("Mat_Valle_Pradera.mat"), mc, go)


def lamina_de_agua(ids):
    """El río. Un plano con el material de agua que ya existía para esta escena."""
    go, tr, mf, mr = ids
    x, z, ex, ez, y = L.AGUA
    return """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: Rio_Agua
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 4294967295
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: %s, y: %s, z: %s}
  m_LocalScale: {x: %s, y: 1, z: %s}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &%d
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Mesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
--- !u!23 &%d
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: %s, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {fileID: 0}""" % (
        go, tr, mf, mr, tr, go, u.f(x), u.f(y), u.f(z), u.f(ex), u.f(ez),
        mf, go, mr, go, u.guid("Mat_Valle_Agua.mat"))


def luz_direccional(ids):
    go, tr, luz, comp = ids
    """Sol SOLO para editar la escena suelta: en partida se apaga solo (ver SolDeEscenaAditiva),
    porque el sol de verdad lo maneja DayNightCycle y dos luces direccionales a la vez es
    exactamente el bug que tenía esta escena."""
    return """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: Luz_Solo_Para_Editar
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0.2903, y: -0.2706, z: 0.0857, w: 0.9134}
  m_LocalPosition: {x: 6000, y: 112, z: 6000}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 36, y: -32, z: 0}
--- !u!108 &%d
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  serializedVersion: 11
  m_Type: 1
  m_Shape: 0
  m_Color: {r: 1, g: 0.94, b: 0.83, a: 1}
  m_Intensity: 1.15
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 2
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 0.85
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {fileID: 0}
  m_DrawHalo: 0
  m_Flare: {fileID: 0}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {x: 1, y: 1}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ForceVisible: 0
  m_ShadowRadius: 0
  m_ShadowAngle: 0
  m_LightUnit: 1
  m_LuxAtDistance: 1
  m_EnableSpotReflector: 1
--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 9d3f21c7a5b04e5d8c1f6a2b3d4e5f60, type: 3}
  m_Name: 
  m_EditorClassIdentifier: """ % (go, tr, luz, comp, tr, go, luz, go, comp, go)


def luz_de_fuego(ids, nombre, pos, padre):
    go, tr, luz = ids
    return """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: %s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: %s, y: %s, z: %s}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: %d}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!108 &%d
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  serializedVersion: 11
  m_Type: 2
  m_Shape: 0
  m_Color: {r: 1, g: 0.62, b: 0.28, a: 1}
  m_Intensity: 3.2
  m_Range: 11
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.8
  m_CookieSize: 10
  m_Shadows:
    m_Type: 0
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {fileID: 0}
  m_DrawHalo: 0
  m_Flare: {fileID: 0}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {x: 1, y: 1}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ForceVisible: 0
  m_ShadowRadius: 0
  m_ShadowAngle: 0
  m_LightUnit: 0
  m_LuxAtDistance: 1
  m_EnableSpotReflector: 1""" % (go, tr, luz, nombre, tr, go, u.f(pos[0]), u.f(pos[1]), u.f(pos[2]),
                                padre, luz, go)


def instancia_actor(e, nombre, g, tr_src, comp_src, go_src, pos, rot):
    qx, qy, qz, qw = u.quat(rot)
    inst = e.nuevo_id()
    strip = e.nuevo_id()
    mods = [
        (comp_src, "persistenceId", nombre),
        (go_src, "m_Name", nombre),
        (tr_src, "m_RootOrder", "0"),
        (tr_src, "m_LocalPosition.x", u.f(pos[0])),
        (tr_src, "m_LocalPosition.y", u.f(pos[1])),
        (tr_src, "m_LocalPosition.z", u.f(pos[2])),
        (tr_src, "m_LocalRotation.x", u.f(qx)),
        (tr_src, "m_LocalRotation.y", u.f(qy)),
        (tr_src, "m_LocalRotation.z", u.f(qz)),
        (tr_src, "m_LocalRotation.w", u.f(qw)),
        (tr_src, "m_LocalEulerAnglesHint.x", "0"),
        (tr_src, "m_LocalEulerAnglesHint.y", u.f(u.euler_de(rot)[1])),
        (tr_src, "m_LocalEulerAnglesHint.z", "0"),
    ]
    cuerpo = "".join(
        "    - target: {fileID: %s, guid: %s, type: 3}\n"
        "      propertyPath: %s\n"
        "      value: %s\n"
        "      objectReference: {fileID: 0}\n" % (d, g, prop, val) for d, prop, val in mods)
    return """--- !u!1001 &%d
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 0}
    m_Modifications:
%s    m_RemovedComponents: []
  m_RemovedGameObjects: []
  m_AddedGameObjects: []
  m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}
--- !u!4 &%d stripped
Transform:
  m_CorrespondingSourceObject: {fileID: %s, guid: %s, type: 3}
  m_PrefabInstance: {fileID: %d}
  m_PrefabAsset: {fileID: 0}""" % (inst, cuerpo, g, strip, tr_src, g, inst)


def objeto_secuencia(ids, hijos, marcas, props):
    go, tr, stage, drv, player, _libre = ids
    lista_hijos = "".join("\n  - {fileID: %d}" % h for h in hijos)
    lista_marcas = "".join("\n  - name: %s\n    target: {fileID: %d}" % (n, marcas[n]) for n, *_ in L.MARCAS)
    lista_props = "".join("\n  - id: %s\n    target: {fileID: %d}\n    eyeHeight: %s" % (pid, props[pid], u.f(alt))
                          for pid, _obj, alt in PROPS if pid in props)
    return """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: SEQ_Prologo_UltimaNoche
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 6000, y: 100, z: 6000}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:%s
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a7a744bc6200a954399ae85b34787b67, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::SequenceStage
  _shots: []
  _props:%s
  _marks:%s
  _cameraDriver: {fileID: %d}
  _distanceMultiplier: 1
  _livePreviewShots: 0
  _modules: []
--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7a1c2a2fbd518144d9e075db607e744e, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::CinematicCameraDriver
  defaultMoveDuration: 0.5
  defaultMoveEase: 4
--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 686d06f3f75bf1c42b8f1cd25ed72e13, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::SequencePlayer
  _signalIn: 
  _signalOut: 
  _actionManager: {fileID: 0}
  _cinematicCamera: {fileID: %d}
  _audioProfile: {fileID: 11400000, guid: 037c4c5ac9007e44cbb40c2065772960, type: 2}
  _sequenceMusicId: 
  _entryTransition: {fileID: 11400000, guid: 013263c83b6a84c4d95beaa8bd163ac8, type: 2}
  _exitTransition: {fileID: 11400000, guid: 013263c83b6a84c4d95beaa8bd163ac8, type: 2}
  _interiorAnchor: {fileID: 0}
  _skipFadeDuration: 0.25
  _simulateHotkey: 0
  _definition: {fileID: 11400000, guid: 15b2368f166245cf92b32079b53da08b, type: 2}
  _stage: {fileID: %d}
  _idleGraceAfterSequence: 3.5
  _skipRevealDuration: 0.25
  _startAtPhase: """ % (go, tr, stage, drv, player, tr, go, lista_hijos,
                        stage, go, lista_props, lista_marcas, drv,
                        drv, go, player, go, drv, stage)


if __name__ == "__main__":
    texto = generar()
    copia = os.path.join(u.RAIZ, "_ClaudeBackups",
                         "Prologo_Valle_%s.unity" % datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))
    os.makedirs(os.path.dirname(copia), exist_ok=True)
    if os.path.exists(DESTINO):
        shutil.copy2(DESTINO, copia)
    open(DESTINO, "w", encoding="utf-8", newline="\n").write(texto)
    print("Escena escrita: %d líneas  (copia de la anterior en %s)" % (texto.count("\n"), os.path.basename(copia)))
