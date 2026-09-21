/**
 * Three.js 3D Pickleball Court, Articulated Player Rig & Paddle Viewport
 * Synchronized with MediaPipe 3D World Landmarks & Biomechanical Kinematics
 */

import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { Landmark3D, PoseAnalysisResult, StrokeMode } from '../types';
import { POSE_LANDMARKS } from '../services/kinematics';
import { generateProPose } from '../services/motionSimulator';
import { Camera, Eye, Move3d, RotateCcw, Sparkles, AlertTriangle, RefreshCw } from 'lucide-react';

interface PickleballScene3DProps {
  landmarks: Landmark3D[];
  worldLandmarks?: Landmark3D[];
  analysis: PoseAnalysisResult;
  mode: StrokeMode;
  isRightHanded: boolean;
  showGhostPro: boolean;
  onToggleGhostPro: () => void;
}

export const PickleballScene3D: React.FC<PickleballScene3DProps> = ({
  landmarks,
  worldLandmarks,
  analysis,
  mode,
  isRightHanded,
  showGhostPro,
  onToggleGhostPro,
}) => {
  const mountRef = useRef<HTMLDivElement>(null);
  const [cameraView, setCameraView] = useState<'slash' | 'front' | 'side' | 'kitchen'>('slash');
  const [webglError, setWebglError] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  // Three.js instances
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);

  // User 3D Rig meshes
  const jointMeshesRef = useRef<THREE.Mesh[]>([]);
  const boneMeshesRef = useRef<THREE.Mesh[]>([]);
  const headMeshRef = useRef<THREE.Mesh | null>(null);
  const paddleGroupRef = useRef<THREE.Group | null>(null);
  const ballMeshRef = useRef<THREE.Mesh | null>(null);
  const kitchenLineLightRef = useRef<THREE.Mesh | null>(null);

  // Ghost Pro Rig meshes
  const ghostJointMeshesRef = useRef<THREE.Mesh[]>([]);
  const ghostBoneMeshesRef = useRef<THREE.Mesh[]>([]);
  const ghostPaddleRef = useRef<THREE.Group | null>(null);

  // Dynamic animation loop time
  const animTimeRef = useRef(0);

  // Court player position anchor (comfortably behind Kitchen line at 2.13m, facing net at z = 0)
  const PLAYER_BASE_X = 0.0;
  const PLAYER_BASE_Y = 0.95; // Hip height in meters above court floor
  const PLAYER_BASE_Z = 2.30; // 2.3m behind net
  const TARGET_LOOKAT = new THREE.Vector3(PLAYER_BASE_X, PLAYER_BASE_Y, PLAYER_BASE_Z);

  // Orbit drag state
  const isDraggingRef = useRef(false);
  const prevPointerRef = useRef({ x: 0, y: 0 });
  const cameraOrbitRef = useRef({
    radius: 4.8,
    theta: Math.PI * 0.28,
    phi: Math.PI * 0.35,
  });

  // Articulated volumetric bones with anatomical thickness (radii in meters)
  interface BoneDef {
    indices: [number, number];
    radius: number;
    type: 'torso' | 'limb' | 'hand' | 'foot';
    name: string;
  }

  const BONE_DEFINITIONS: BoneDef[] = [
    // --- Thân người (Torso & Pelvis - Khối xương dày, lực lưỡng) ---
    { indices: [POSE_LANDMARKS.LEFT_SHOULDER, POSE_LANDMARKS.RIGHT_SHOULDER], radius: 0.052, type: 'torso', name: 'Vai' },
    { indices: [POSE_LANDMARKS.LEFT_SHOULDER, POSE_LANDMARKS.LEFT_HIP], radius: 0.048, type: 'torso', name: 'Lưng trái' },
    { indices: [POSE_LANDMARKS.RIGHT_SHOULDER, POSE_LANDMARKS.RIGHT_HIP], radius: 0.048, type: 'torso', name: 'Lưng phải' },
    { indices: [POSE_LANDMARKS.LEFT_HIP, POSE_LANDMARKS.RIGHT_HIP], radius: 0.052, type: 'torso', name: 'Khung chậu' },
    { indices: [POSE_LANDMARKS.LEFT_SHOULDER, POSE_LANDMARKS.NOSE], radius: 0.036, type: 'torso', name: 'Cổ vai trái' },
    { indices: [POSE_LANDMARKS.RIGHT_SHOULDER, POSE_LANDMARKS.NOSE], radius: 0.036, type: 'torso', name: 'Cổ vai phải' },

    // --- Chi trên - Cánh tay trái ---
    { indices: [POSE_LANDMARKS.LEFT_SHOULDER, POSE_LANDMARKS.LEFT_ELBOW], radius: 0.044, type: 'limb', name: 'Bắp tay trái' },
    { indices: [POSE_LANDMARKS.LEFT_ELBOW, POSE_LANDMARKS.LEFT_WRIST], radius: 0.038, type: 'limb', name: 'Cẳng tay trái' },

    // --- Đốt xương bàn tay trái (Left Hand Digits) ---
    { indices: [POSE_LANDMARKS.LEFT_WRIST, POSE_LANDMARKS.LEFT_THUMB], radius: 0.024, type: 'hand', name: 'Đốt ngón cái trái' },
    { indices: [POSE_LANDMARKS.LEFT_WRIST, POSE_LANDMARKS.LEFT_INDEX], radius: 0.026, type: 'hand', name: 'Đốt ngón trỏ trái' },
    { indices: [POSE_LANDMARKS.LEFT_WRIST, POSE_LANDMARKS.LEFT_PINKY], radius: 0.022, type: 'hand', name: 'Đốt ngón út trái' },
    { indices: [POSE_LANDMARKS.LEFT_INDEX, POSE_LANDMARKS.LEFT_PINKY], radius: 0.020, type: 'hand', name: 'Vòm xương bàn tay trái' },
    { indices: [POSE_LANDMARKS.LEFT_INDEX, POSE_LANDMARKS.LEFT_THUMB], radius: 0.018, type: 'hand', name: 'Kẽ ngón cái-trỏ trái' },

    // --- Chi trên - Cánh tay phải (Cầm vợt Pickleball) ---
    { indices: [POSE_LANDMARKS.RIGHT_SHOULDER, POSE_LANDMARKS.RIGHT_ELBOW], radius: 0.044, type: 'limb', name: 'Bắp tay phải' },
    { indices: [POSE_LANDMARKS.RIGHT_ELBOW, POSE_LANDMARKS.RIGHT_WRIST], radius: 0.038, type: 'limb', name: 'Cẳng tay phải' },

    // --- Đốt xương bàn tay phải (Right Hand Digits) ---
    { indices: [POSE_LANDMARKS.RIGHT_WRIST, POSE_LANDMARKS.RIGHT_THUMB], radius: 0.024, type: 'hand', name: 'Đốt ngón cái phải' },
    { indices: [POSE_LANDMARKS.RIGHT_WRIST, POSE_LANDMARKS.RIGHT_INDEX], radius: 0.026, type: 'hand', name: 'Đốt ngón trỏ phải' },
    { indices: [POSE_LANDMARKS.RIGHT_WRIST, POSE_LANDMARKS.RIGHT_PINKY], radius: 0.022, type: 'hand', name: 'Đốt ngón út phải' },
    { indices: [POSE_LANDMARKS.RIGHT_INDEX, POSE_LANDMARKS.RIGHT_PINKY], radius: 0.020, type: 'hand', name: 'Vòm xương bàn tay phải' },
    { indices: [POSE_LANDMARKS.RIGHT_INDEX, POSE_LANDMARKS.RIGHT_THUMB], radius: 0.018, type: 'hand', name: 'Kẽ ngón cái-trỏ phải' },

    // --- Chi dưới - Chân trái ---
    { indices: [POSE_LANDMARKS.LEFT_HIP, POSE_LANDMARKS.LEFT_KNEE], radius: 0.056, type: 'limb', name: 'Xương đùi trái' },
    { indices: [POSE_LANDMARKS.LEFT_KNEE, POSE_LANDMARKS.LEFT_ANKLE], radius: 0.048, type: 'limb', name: 'Cẳng chân trái' },

    // --- Đốt xương bàn chân trái (Left Foot Digits) ---
    { indices: [POSE_LANDMARKS.LEFT_ANKLE, POSE_LANDMARKS.LEFT_HEEL], radius: 0.038, type: 'foot', name: 'Gót chân trái' },
    { indices: [POSE_LANDMARKS.LEFT_ANKLE, POSE_LANDMARKS.LEFT_FOOT_INDEX], radius: 0.035, type: 'foot', name: 'Mu bàn chân trái' },
    { indices: [POSE_LANDMARKS.LEFT_HEEL, POSE_LANDMARKS.LEFT_FOOT_INDEX], radius: 0.032, type: 'foot', name: 'Đế lòng bàn chân trái' },

    // --- Chi dưới - Chân phải ---
    { indices: [POSE_LANDMARKS.RIGHT_HIP, POSE_LANDMARKS.RIGHT_KNEE], radius: 0.056, type: 'limb', name: 'Xương đùi phải' },
    { indices: [POSE_LANDMARKS.RIGHT_KNEE, POSE_LANDMARKS.RIGHT_ANKLE], radius: 0.048, type: 'limb', name: 'Cẳng chân phải' },

    // --- Đốt xương bàn chân phải (Right Foot Digits) ---
    { indices: [POSE_LANDMARKS.RIGHT_ANKLE, POSE_LANDMARKS.RIGHT_HEEL], radius: 0.038, type: 'foot', name: 'Gót chân phải' },
    { indices: [POSE_LANDMARKS.RIGHT_ANKLE, POSE_LANDMARKS.RIGHT_FOOT_INDEX], radius: 0.035, type: 'foot', name: 'Mu bàn chân phải' },
    { indices: [POSE_LANDMARKS.RIGHT_HEEL, POSE_LANDMARKS.RIGHT_FOOT_INDEX], radius: 0.032, type: 'foot', name: 'Đế lòng bàn chân phải' },
  ];

  // Helper to calculate joint sphere radius based on anatomical hierarchy
  const getJointRadius = (index: number): number => {
    // Large ball joints: Hips (23, 24), Shoulders (11, 12)
    if (index === 23 || index === 24 || index === 11 || index === 12) return 0.058;
    // Knees (25, 26), Elbows (13, 14)
    if (index === 25 || index === 26 || index === 13 || index === 14) return 0.052;
    // Wrists (15, 16), Ankles (27, 28)
    if (index === 15 || index === 16 || index === 27 || index === 28) return 0.046;
    // Hand finger joints (Pinky 17/18, Index 19/20, Thumb 21/22)
    if (index >= 17 && index <= 22) return 0.026;
    // Foot joints (Heel 29/30, Foot index 31/32)
    if (index >= 29 && index <= 32) return 0.036;
    // Nose / Center head
    if (index === 0) return 0.038;
    return 0.020;
  };

  // Helper to build a pickleball paddle 3D mesh
  const createPaddleModel = (isGhost = false) => {
    const group = new THREE.Group();

    // 1. Grip handle
    const handleGeo = new THREE.CylinderGeometry(0.022, 0.024, 0.22, 16);
    const handleMat = new THREE.MeshStandardMaterial({
      color: isGhost ? 0x00ffff : 0x0f172a,
      roughness: 0.8,
      transparent: isGhost,
      opacity: isGhost ? 0.45 : 1,
    });
    const handle = new THREE.Mesh(handleGeo, handleMat);
    handle.position.y = 0.1;
    group.add(handle);

    // Grip rings
    const ringGeo = new THREE.TorusGeometry(0.025, 0.003, 8, 20);
    const ringMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0 });
    const ring = new THREE.Mesh(ringGeo, ringMat);
    ring.rotation.x = Math.PI / 2;
    ring.position.y = 0.2;
    group.add(ring);

    // 2. Paddle Face (Oblong rounded paddle face)
    const faceShape = new THREE.Shape();
    const w = 0.11;
    const h = 0.24;
    const r = 0.05;
    faceShape.moveTo(-w + r, -h / 2);
    faceShape.lineTo(w - r, -h / 2);
    faceShape.quadraticCurveTo(w, -h / 2, w, -h / 2 + r);
    faceShape.lineTo(w, h / 2 - r);
    faceShape.quadraticCurveTo(w, h / 2, w - r, h / 2);
    faceShape.lineTo(-w + r, h / 2);
    faceShape.quadraticCurveTo(-w, h / 2, -w, h / 2 - r);
    faceShape.lineTo(-w, -h / 2 + r);
    faceShape.quadraticCurveTo(-w, -h / 2, -w + r, -h / 2);

    const extrudeSettings = {
      depth: 0.016,
      bevelEnabled: true,
      bevelSegments: 4,
      steps: 1,
      bevelSize: 0.005,
      bevelThickness: 0.005,
    };
    const faceGeo = new THREE.ExtrudeGeometry(faceShape, extrudeSettings);
    const faceMat = new THREE.MeshStandardMaterial({
      color: isGhost ? 0x00e5ff : 0x0284c7, // Neon cyan for ghost, Pro Cyan for player
      roughness: 0.35,
      metalness: 0.2,
      transparent: isGhost,
      opacity: isGhost ? 0.5 : 1,
    });
    const face = new THREE.Mesh(faceGeo, faceMat);
    face.position.y = 0.34;
    group.add(face);

    // 3. Edge guard
    const edgeMat = new THREE.MeshStandardMaterial({
      color: 0x0f172a,
      roughness: 0.5,
    });
    const edgeGeo = new THREE.TorusGeometry(0.12, 0.008, 8, 32);
    const edge = new THREE.Mesh(edgeGeo, edgeMat);
    edge.position.y = 0.34;
    edge.scale.set(1.0, 1.15, 1.0);
    group.add(edge);

    return group;
  };

  // Convert real-world MediaPipe landmarks to 3D Three.js court points
  const computePosePoints3D = (
    lms: Landmark3D[],
    worldLms?: Landmark3D[],
    isGhost = false
  ): THREE.Vector3[] => {
    const points: THREE.Vector3[] = [];
    const baseX = isGhost ? 0.45 : PLAYER_BASE_X;
    const baseY = PLAYER_BASE_Y;
    const baseZ = PLAYER_BASE_Z;

    // Detect if lower body is visible
    const hasKnees =
      (lms[25]?.visibility ?? 0) > 0.45 && (lms[26]?.visibility ?? 0) > 0.45;
    const hasAnkles =
      (lms[27]?.visibility ?? 0) > 0.45 &&
      (lms[28]?.visibility ?? 0) > 0.45 &&
      lms[27].y < 0.96 &&
      lms[28].y < 0.96;
    const isFullBody = hasKnees && hasAnkles;

    if (worldLms && worldLms.length >= 33) {
      // Metric real-world 3D coordinates (origin at hip center in meters)
      // Mirror check: ensure right shoulder (12) maps to athlete's right (+X)
      const mirrorSign = worldLms[12].x < worldLms[11].x ? -1.0 : 1.0;

      // Dynamic hip height when legs visible
      let hipHeight = baseY;
      if (isFullBody && worldLms[27] && worldLms[28]) {
        const legLength = Math.max(0.7, (worldLms[27].y + worldLms[28].y) / 2);
        hipHeight = Math.max(0.65, Math.min(1.05, legLength));
      }

      for (let i = 0; i < 33; i++) {
        if (!isFullBody && i >= 25) {
          // Half-body (sitting/desk): synthesize grounded athletic ready stance on court floor!
          if (i === 25) {
            points.push(new THREE.Vector3(baseX - 0.22, hipHeight * 0.52, baseZ - 0.08));
          } else if (i === 26) {
            points.push(new THREE.Vector3(baseX + 0.22, hipHeight * 0.52, baseZ - 0.08));
          } else if (i === 27) {
            points.push(new THREE.Vector3(baseX - 0.26, 0.06, baseZ + 0.02));
          } else if (i === 28) {
            points.push(new THREE.Vector3(baseX + 0.26, 0.06, baseZ + 0.02));
          } else if (i === 29) {
            // Left heel
            points.push(new THREE.Vector3(baseX - 0.26, 0.04, baseZ + 0.08));
          } else if (i === 30) {
            // Right heel
            points.push(new THREE.Vector3(baseX + 0.26, 0.04, baseZ + 0.08));
          } else if (i === 31) {
            // Left toe
            points.push(new THREE.Vector3(baseX - 0.26, 0.02, baseZ - 0.12));
          } else if (i === 32) {
            // Right toe
            points.push(new THREE.Vector3(baseX + 0.26, 0.02, baseZ - 0.12));
          } else {
            points.push(new THREE.Vector3(baseX, 0.05, baseZ));
          }
        } else {
          const w = worldLms[i];
          const jX = baseX + w.x * mirrorSign;
          const jY = Math.max(0.04, hipHeight - w.y);
          const jZ = baseZ - w.z;
          points.push(new THREE.Vector3(jX, jY, jZ));
        }
      }
    } else {
      // Normalized camera landmarks fallback with metric scaling
      const hipX = (lms[23].x + lms[24].x) / 2;
      const hipY = (lms[23].y + lms[24].y) / 2;
      const shoulderSpan = Math.hypot(lms[11].x - lms[12].x, lms[11].y - lms[12].y);
      const scale = 0.42 / Math.max(0.08, shoulderSpan);
      const mirrorSign = lms[12].x < lms[11].x ? -1.0 : 1.0;

      for (let i = 0; i < 33; i++) {
        if (!isFullBody && i >= 25) {
          if (i === 25) points.push(new THREE.Vector3(baseX - 0.22, baseY * 0.52, baseZ - 0.08));
          else if (i === 26) points.push(new THREE.Vector3(baseX + 0.22, baseY * 0.52, baseZ - 0.08));
          else if (i === 27) points.push(new THREE.Vector3(baseX - 0.26, 0.06, baseZ + 0.02));
          else if (i === 28) points.push(new THREE.Vector3(baseX + 0.26, 0.06, baseZ + 0.02));
          else if (i === 29) points.push(new THREE.Vector3(baseX - 0.26, 0.04, baseZ + 0.08));
          else if (i === 30) points.push(new THREE.Vector3(baseX + 0.26, 0.04, baseZ + 0.08));
          else if (i === 31) points.push(new THREE.Vector3(baseX - 0.26, 0.02, baseZ - 0.12));
          else if (i === 32) points.push(new THREE.Vector3(baseX + 0.26, 0.02, baseZ - 0.12));
          else points.push(new THREE.Vector3(baseX, 0.05, baseZ));
        } else {
          const lm = lms[i];
          const jX = baseX + (lm.x - hipX) * scale * mirrorSign;
          const jY = Math.max(0.04, baseY - (lm.y - hipY) * scale);
          const jZ = baseZ - (lm.z || 0) * scale * 1.2;
          points.push(new THREE.Vector3(jX, jY, jZ));
        }
      }
    }

    return points;
  };

  useEffect(() => {
    if (!mountRef.current) return;
    const container = mountRef.current;
    const width = container.clientWidth;
    const height = container.clientHeight;

    // 1. Scene
    const scene = new THREE.Scene();
    sceneRef.current = scene;
    scene.background = new THREE.Color(0x090d16); // Deep athletic night court
    scene.fog = new THREE.FogExp2(0x090d16, 0.06);

    // 2. Camera
    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 50);
    cameraRef.current = camera;
    updateCameraPosition();

    // 3. Safe WebGL Renderer Initialization
    let renderer: THREE.WebGLRenderer;
    try {
      const canvas = document.createElement('canvas');
      const gl =
        canvas.getContext('webgl2', { alpha: true, antialias: true }) ||
        canvas.getContext('webgl', { alpha: true, antialias: true }) ||
        canvas.getContext('experimental-webgl');
      if (!gl) {
        throw new Error('WebGL context unavailable in browser.');
      }

      renderer = new THREE.WebGLRenderer({
        canvas,
        antialias: true,
        alpha: true,
        powerPreference: 'default',
        failIfMajorPerformanceCaveat: false,
      });
      rendererRef.current = renderer;
      renderer.setSize(width, height);
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
      renderer.shadowMap.enabled = true;
      renderer.shadowMap.type = THREE.PCFSoftShadowMap;
      container.appendChild(renderer.domElement);
      setWebglError(null);
    } catch (err: any) {
      console.warn('WebGL context creation error:', err);
      setWebglError(
        'Trình duyệt hoặc GPU đang tạm thời khóa ngữ cảnh WebGL đồ họa. Hệ thống đang đợi giải phóng bộ nhớ.'
      );
      return;
    }

    // 4. Lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.85);
    scene.add(ambientLight);

    const stadiumLight1 = new THREE.DirectionalLight(0xffffff, 1.6);
    stadiumLight1.position.set(5, 8, 6);
    stadiumLight1.castShadow = true;
    stadiumLight1.shadow.mapSize.width = 1024;
    stadiumLight1.shadow.mapSize.height = 1024;
    scene.add(stadiumLight1);

    const stadiumLight2 = new THREE.DirectionalLight(0x38bdf8, 0.8);
    stadiumLight2.position.set(-6, 6, -4);
    scene.add(stadiumLight2);

    // 5. Pickleball Court Construction (USA Pickleball Official Dimensions)
    const courtWidth = 6.1; // 20 feet
    const courtLength = 13.4; // 44 feet
    const nvzLength = 2.13; // 7 feet kitchen on each side

    const courtPerimeterGeo = new THREE.PlaneGeometry(12, 18);
    const courtPerimeterMat = new THREE.MeshStandardMaterial({
      color: 0x1e293b,
      roughness: 0.9,
    });
    const courtPerimeter = new THREE.Mesh(courtPerimeterGeo, courtPerimeterMat);
    courtPerimeter.rotation.x = -Math.PI / 2;
    courtPerimeter.receiveShadow = true;
    scene.add(courtPerimeter);

    // Main Court Infield (Vibrant Pro Blue)
    const courtGeo = new THREE.PlaneGeometry(courtWidth, courtLength);
    const courtMat = new THREE.MeshStandardMaterial({
      color: 0x0284c7, // Vibrant Pro Court Blue
      roughness: 0.7,
    });
    const court = new THREE.Mesh(courtGeo, courtMat);
    court.rotation.x = -Math.PI / 2;
    court.position.y = 0.005;
    court.receiveShadow = true;
    scene.add(court);

    // Non-Volley Zone / Kitchen (Emerald Green)
    const nvzGeo = new THREE.PlaneGeometry(courtWidth, nvzLength * 2);
    const nvzMat = new THREE.MeshStandardMaterial({
      color: 0x059669, // Vibrant Emerald Kitchen
      roughness: 0.7,
    });
    const nvz = new THREE.Mesh(nvzGeo, nvzMat);
    nvz.rotation.x = -Math.PI / 2;
    nvz.position.y = 0.008;
    nvz.receiveShadow = true;
    scene.add(nvz);

    // Court White Lines (2 inches / 0.05m wide)
    const lineMat = new THREE.MeshBasicMaterial({ color: 0xffffff });
    const createCourtLine = (w: number, l: number, x: number, z: number) => {
      const geo = new THREE.PlaneGeometry(w, l);
      const line = new THREE.Mesh(geo, lineMat);
      line.rotation.x = -Math.PI / 2;
      line.position.set(x, 0.01, z);
      scene.add(line);
      return line;
    };

    // Sidelines & Baselines
    createCourtLine(courtWidth, 0.06, 0, courtLength / 2); // Baseline 1
    createCourtLine(courtWidth, 0.06, 0, -courtLength / 2); // Baseline 2
    createCourtLine(0.06, courtLength, courtWidth / 2, 0); // Sideline Right
    createCourtLine(0.06, courtLength, -courtWidth / 2, 0); // Sideline Left

    // Kitchen Lines (7 ft / 2.13m from net)
    createCourtLine(courtWidth, 0.06, 0, nvzLength);
    createCourtLine(courtWidth, 0.06, 0, -nvzLength);

    // Centerlines
    createCourtLine(0.06, courtLength / 2 - nvzLength, 0, (courtLength / 2 + nvzLength) / 2);
    createCourtLine(0.06, courtLength / 2 - nvzLength, 0, -(courtLength / 2 + nvzLength) / 2);

    // Glowing Kitchen Line Warning Light (illuminates red on fault)
    const kitchenGlowGeo = new THREE.BoxGeometry(courtWidth, 0.02, 0.08);
    const kitchenGlowMat = new THREE.MeshBasicMaterial({
      color: 0x10b981,
      transparent: true,
      opacity: 0.6,
    });
    const kitchenGlow = new THREE.Mesh(kitchenGlowGeo, kitchenGlowMat);
    kitchenGlow.position.set(0, 0.015, nvzLength);
    scene.add(kitchenGlow);
    kitchenLineLightRef.current = kitchenGlow;

    // Net Construction (34 inches / 0.86m high in center, 36 inches / 0.91m at posts)
    const netPostGeo = new THREE.CylinderGeometry(0.04, 0.04, 0.95, 16);
    const netPostMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.3 });
    const postLeft = new THREE.Mesh(netPostGeo, netPostMat);
    postLeft.position.set(-courtWidth / 2 - 0.15, 0.47, 0);
    scene.add(postLeft);
    const postRight = new THREE.Mesh(netPostGeo, netPostMat);
    postRight.position.set(courtWidth / 2 + 0.15, 0.47, 0);
    scene.add(postRight);

    // Net mesh
    const netMeshGeo = new THREE.PlaneGeometry(courtWidth + 0.3, 0.88);
    const netMeshMat = new THREE.MeshStandardMaterial({
      color: 0x1e293b,
      roughness: 0.9,
      transparent: true,
      opacity: 0.65,
      side: THREE.DoubleSide,
      wireframe: true,
    });
    const net = new THREE.Mesh(netMeshGeo, netMeshMat);
    net.position.set(0, 0.44, 0);
    scene.add(net);

    // Top white tape of net
    const netTapeGeo = new THREE.BoxGeometry(courtWidth + 0.3, 0.05, 0.04);
    const netTape = new THREE.Mesh(netTapeGeo, lineMat);
    netTape.position.set(0, 0.88, 0);
    scene.add(netTape);

    // 6. Pickleball Ball 3D Model
    const ballGeo = new THREE.SphereGeometry(0.06, 24, 24);
    const ballMat = new THREE.MeshStandardMaterial({
      color: 0xfacc15, // Neon Tour Yellow
      roughness: 0.3,
      metalness: 0.1,
    });
    const ball = new THREE.Mesh(ballGeo, ballMat);
    ball.castShadow = true;
    ball.position.set(0.4, 0.7, 1.6);
    scene.add(ball);
    ballMeshRef.current = ball;

    // 7. Human 3D Athlete Rig (Volumetric Joint Spheres & Solid Bone Cylinders)
    const jointMat = new THREE.MeshStandardMaterial({
      color: 0x10b981, // Athletic Emerald green
      roughness: 0.25,
      metalness: 0.3,
    });
    const boneTorsoMat = new THREE.MeshStandardMaterial({
      color: 0x0284c7, // Vibrant athletic cyan/pro court blue
      roughness: 0.35,
      metalness: 0.25,
    });
    const boneLimbMat = new THREE.MeshStandardMaterial({
      color: 0x0ea5e9, // Bright athletic ocean blue
      roughness: 0.3,
      metalness: 0.2,
    });
    const boneHandMat = new THREE.MeshStandardMaterial({
      color: 0x38bdf8, // Electric sky blue hand digits
      roughness: 0.25,
      metalness: 0.2,
    });
    const boneFootMat = new THREE.MeshStandardMaterial({
      color: 0x06b6d4, // Vivid turquoise foot digits & soles
      roughness: 0.3,
      metalness: 0.2,
    });

    const jointMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < 33; i++) {
      const radius = getJointRadius(i);
      const jGeo = new THREE.SphereGeometry(radius, 16, 16);
      const jMesh = new THREE.Mesh(jGeo, jointMat.clone());
      jMesh.castShadow = true;
      scene.add(jMesh);
      jointMeshes.push(jMesh);
    }
    jointMeshesRef.current = jointMeshes;

    // Head with visor
    const headGroup = new THREE.Group();
    const headGeo = new THREE.SphereGeometry(0.12, 24, 24);
    const headMat = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.45 });
    const headSphere = new THREE.Mesh(headGeo, headMat);
    headGroup.add(headSphere);

    const visorGeo = new THREE.BoxGeometry(0.15, 0.02, 0.13);
    const visorMat = new THREE.MeshStandardMaterial({ color: 0x0284c7 });
    const visor = new THREE.Mesh(visorGeo, visorMat);
    visor.position.set(0, 0.04, -0.10);
    headGroup.add(visor);

    scene.add(headGroup);
    headMeshRef.current = headSphere;

    // Solid volumetric bone cylinders
    const boneMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < BONE_DEFINITIONS.length; i++) {
      const def = BONE_DEFINITIONS[i];
      const bGeo = new THREE.CylinderGeometry(def.radius, def.radius, 1, 16);
      let mat = boneLimbMat;
      if (def.type === 'torso') mat = boneTorsoMat;
      else if (def.type === 'hand') mat = boneHandMat;
      else if (def.type === 'foot') mat = boneFootMat;

      const bMesh = new THREE.Mesh(bGeo, mat);
      bMesh.castShadow = true;
      scene.add(bMesh);
      boneMeshes.push(bMesh);
    }
    boneMeshesRef.current = boneMeshes;

    // Mount Pickleball Paddle to User
    const paddle = createPaddleModel(false);
    scene.add(paddle);
    paddleGroupRef.current = paddle;

    // 8. Ghost Pro Athlete Rig (Hologram Overlay)
    const ghostJointMat = new THREE.MeshStandardMaterial({
      color: 0xf59e0b, // Amber Gold Hologram
      roughness: 0.2,
      transparent: true,
      opacity: 0.75,
    });
    const ghostBoneMat = new THREE.MeshStandardMaterial({
      color: 0xfbbf24,
      roughness: 0.3,
      transparent: true,
      opacity: 0.55,
    });

    const ghostJointMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < 33; i++) {
      const radius = getJointRadius(i) * 0.9;
      const gGeo = new THREE.SphereGeometry(radius, 16, 16);
      const gMesh = new THREE.Mesh(gGeo, ghostJointMat);
      gMesh.visible = showGhostPro;
      scene.add(gMesh);
      ghostJointMeshes.push(gMesh);
    }
    ghostJointMeshesRef.current = ghostJointMeshes;

    const ghostBoneMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < BONE_DEFINITIONS.length; i++) {
      const def = BONE_DEFINITIONS[i];
      const gbGeo = new THREE.CylinderGeometry(def.radius * 0.85, def.radius * 0.85, 1, 16);
      const gbMesh = new THREE.Mesh(gbGeo, ghostBoneMat);
      gbMesh.visible = showGhostPro;
      scene.add(gbMesh);
      ghostBoneMeshes.push(gbMesh);
    }
    ghostBoneMeshesRef.current = ghostBoneMeshes;

    const ghostPaddle = createPaddleModel(true);
    ghostPaddle.visible = showGhostPro;
    scene.add(ghostPaddle);
    ghostPaddleRef.current = ghostPaddle;

    // 9. Interactive Orbit Controls (Pointer Drag)
    const onPointerDown = (e: MouseEvent | TouchEvent) => {
      isDraggingRef.current = true;
      const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
      const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;
      prevPointerRef.current = { x: clientX, y: clientY };
    };

    const onPointerMove = (e: MouseEvent | TouchEvent) => {
      if (!isDraggingRef.current) return;
      const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
      const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;
      const dx = clientX - prevPointerRef.current.x;
      const dy = clientY - prevPointerRef.current.y;
      prevPointerRef.current = { x: clientX, y: clientY };

      cameraOrbitRef.current.theta -= dx * 0.008;
      cameraOrbitRef.current.phi = Math.max(
        0.2,
        Math.min(Math.PI / 2 - 0.05, cameraOrbitRef.current.phi - dy * 0.008)
      );
      updateCameraPosition();
    };

    const onPointerUp = () => {
      isDraggingRef.current = false;
    };

    const dom = renderer.domElement;

    // WebGL Context Lost & Restored Handlers
    const handleContextLost = (e: Event) => {
      e.preventDefault();
      console.warn('Pickleball 3D: WebGL Context Lost');
      setWebglError('Ngữ cảnh đồ họa 3D bị gián đoạn. Nhấn nút bên dưới để khôi phục.');
    };

    const handleContextRestored = () => {
      console.log('Pickleball 3D: WebGL Context Restored');
      setWebglError(null);
      setRetryCount(c => c + 1);
    };

    dom.addEventListener('webglcontextlost', handleContextLost, false);
    dom.addEventListener('webglcontextrestored', handleContextRestored, false);

    dom.addEventListener('mousedown', onPointerDown);
    window.addEventListener('mousemove', onPointerMove);
    window.addEventListener('mouseup', onPointerUp);
    dom.addEventListener('touchstart', onPointerDown, { passive: true });
    window.addEventListener('touchmove', onPointerMove, { passive: true });
    window.addEventListener('touchend', onPointerUp);

    // 10. Resize Observer
    const resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        const { width: newW, height: newH } = entry.contentRect;
        if (newW > 0 && newH > 0) {
          camera.aspect = newW / newH;
          camera.updateProjectionMatrix();
          renderer.setSize(newW, newH);
        }
      }
    });
    resizeObserver.observe(container);

    // 11. Animation Render Loop
    let reqId: number;
    const animate = () => {
      reqId = requestAnimationFrame(animate);
      animTimeRef.current += 0.016;

      // Dynamic ball bouncing/swing trajectory near player striking zone
      if (ballMeshRef.current) {
        const t = animTimeRef.current;
        const bounceH = Math.abs(Math.sin(t * 3.5)) * 0.45;
        ballMeshRef.current.position.y = 0.08 + bounceH;
        ballMeshRef.current.position.z = 1.6 + Math.cos(t * 1.5) * 0.3;
        ballMeshRef.current.rotation.x += 0.04;
        ballMeshRef.current.rotation.y += 0.03;
      }

      renderer.render(scene, camera);
    };
    animate();

    return () => {
      cancelAnimationFrame(reqId);
      resizeObserver.disconnect();
      dom.removeEventListener('webglcontextlost', handleContextLost);
      dom.removeEventListener('webglcontextrestored', handleContextRestored);
      dom.removeEventListener('mousedown', onPointerDown);
      window.removeEventListener('mousemove', onPointerMove);
      window.removeEventListener('mouseup', onPointerUp);
      dom.removeEventListener('touchstart', onPointerDown);
      window.removeEventListener('touchmove', onPointerMove);
      window.removeEventListener('touchend', onPointerUp);
      if (container && dom && container.contains(dom)) {
        container.removeChild(dom);
      }
      try {
        renderer.dispose();
        // Immediately notify browser GPU to release context allocation
        renderer.forceContextLoss();
      } catch (e) {
        // safe
      }
      rendererRef.current = null;
      sceneRef.current = null;
      cameraRef.current = null;
    };
  }, [retryCount]);

  // Update Camera based on Orbit spherical coordinates around the athlete
  const updateCameraPosition = () => {
    if (!cameraRef.current) return;
    const { radius, theta, phi } = cameraOrbitRef.current;
    const x = radius * Math.sin(phi) * Math.sin(theta);
    const y = radius * Math.cos(phi);
    const z = radius * Math.sin(phi) * Math.cos(theta);

    cameraRef.current.position.set(
      TARGET_LOOKAT.x + x,
      TARGET_LOOKAT.y + y,
      TARGET_LOOKAT.z + z
    );
    cameraRef.current.lookAt(TARGET_LOOKAT);
  };

  // Switch camera view presets
  const handleViewSelect = (view: 'slash' | 'front' | 'side' | 'kitchen') => {
    setCameraView(view);
    if (view === 'slash') {
      // 45 degree rear angle looking over shoulder towards net
      cameraOrbitRef.current = { radius: 4.8, theta: Math.PI * 0.28, phi: Math.PI * 0.35 };
    } else if (view === 'front') {
      // Frontal facing view
      cameraOrbitRef.current = { radius: 4.4, theta: Math.PI, phi: Math.PI * 0.36 };
    } else if (view === 'side') {
      // Profile side view for low-to-high swing stroke
      cameraOrbitRef.current = { radius: 4.6, theta: Math.PI * 0.5, phi: Math.PI * 0.38 };
    } else if (view === 'kitchen') {
      // Top-down down-the-line kitchen NVZ view
      cameraOrbitRef.current = { radius: 4.2, theta: Math.PI * 0.12, phi: 0.45 };
    }
    updateCameraPosition();
  };

  // Synchronize 3D Joint positions with real-time MediaPipe landmarks
  useEffect(() => {
    if (!landmarks || landmarks.length < 33 || jointMeshesRef.current.length < 33) return;

    // Compute metric 3D point array from World Landmarks or scaled normalized landmarks
    const points3D = computePosePoints3D(landmarks, worldLandmarks, false);

    for (let i = 0; i < 33; i++) {
      const v = points3D[i];
      const jMesh = jointMeshesRef.current[i];
      if (jMesh && v) {
        jMesh.position.copy(v);

        // Highlight error joints with Neon Red/Amber
        const isErrorJoint =
          (analysis.errors.some(e => e.joint === 'knee') && (i === 25 || i === 26)) ||
          (analysis.errors.some(e => e.joint === 'elbow') && (i === 13 || i === 14)) ||
          (analysis.errors.some(e => e.joint === 'wrist') && (i === 15 || i === 16)) ||
          (analysis.errors.some(e => e.joint === 'footwork') && (i === 27 || i === 28));

        const mat = jMesh.material as THREE.MeshStandardMaterial;
        if (isErrorJoint) {
          mat.color.setHex(0xef4444); // Neon Red Error
          jMesh.scale.set(1.4, 1.4, 1.4);
        } else {
          mat.color.setHex(analysis.score >= 85 ? 0x10b981 : 0xf59e0b); // Emerald or Amber
          jMesh.scale.set(1.0, 1.0, 1.0);
        }
      }
    }

    // Position Head & Visor
    if (headMeshRef.current && headMeshRef.current.parent) {
      const noseV = points3D[POSE_LANDMARKS.NOSE];
      if (noseV) {
        headMeshRef.current.parent.position.set(noseV.x, noseV.y + 0.02, noseV.z);
      }
    }

    // Orient bone cylinders between joints
    for (let b = 0; b < BONE_DEFINITIONS.length; b++) {
      const [idxA, idxB] = BONE_DEFINITIONS[b].indices;
      const pA = points3D[idxA];
      const pB = points3D[idxB];
      const boneMesh = boneMeshesRef.current[b];
      if (boneMesh && pA && pB) {
        const mid = new THREE.Vector3().addVectors(pA, pB).multiplyScalar(0.5);
        boneMesh.position.copy(mid);

        const dir = new THREE.Vector3().subVectors(pB, pA);
        const length = dir.length();
        boneMesh.scale.set(1, Math.max(0.01, length), 1);

        // Align cylinder with vector
        dir.normalize();
        const up = new THREE.Vector3(0, 1, 0);
        const quaternion = new THREE.Quaternion().setFromUnitVectors(up, dir);
        boneMesh.setRotationFromQuaternion(quaternion);
      }
    }

    // Mount Pickleball Paddle to dominant hand
    const domWristIdx = isRightHanded ? POSE_LANDMARKS.RIGHT_WRIST : POSE_LANDMARKS.LEFT_WRIST;
    const domElbowIdx = isRightHanded ? POSE_LANDMARKS.RIGHT_ELBOW : POSE_LANDMARKS.LEFT_ELBOW;
    const domIndexIdx = isRightHanded ? POSE_LANDMARKS.RIGHT_INDEX : POSE_LANDMARKS.LEFT_INDEX;

    if (paddleGroupRef.current && points3D[domWristIdx] && points3D[domElbowIdx]) {
      const wristV = points3D[domWristIdx];
      const elbowV = points3D[domElbowIdx];
      const indexV = points3D[domIndexIdx] || wristV;

      paddleGroupRef.current.position.copy(wristV);

      // Forearm direction
      const forearmDir = new THREE.Vector3().subVectors(wristV, elbowV).normalize();
      const handDir = new THREE.Vector3().subVectors(indexV, wristV).normalize();
      const combinedDir = forearmDir.clone().add(handDir).normalize();

      const up = new THREE.Vector3(0, 1, 0);
      const paddleQuat = new THREE.Quaternion().setFromUnitVectors(up, combinedDir);
      paddleGroupRef.current.setRotationFromQuaternion(paddleQuat);
    }

    // Kitchen line fault alert glow
    if (kitchenLineLightRef.current) {
      const mat = kitchenLineLightRef.current.material as THREE.MeshBasicMaterial;
      if (analysis.isKitchenViolated) {
        mat.color.setHex(0xef4444);
        mat.opacity = 0.95;
      } else {
        mat.color.setHex(0x10b981);
        mat.opacity = 0.5;
      }
    }
  }, [landmarks, worldLandmarks, analysis, isRightHanded]);

  // Synchronize Ghost "Pro Form" Hologram overlay
  useEffect(() => {
    const isVisible = showGhostPro;
    ghostJointMeshesRef.current.forEach(m => (m.visible = isVisible));
    ghostBoneMeshesRef.current.forEach(m => (m.visible = isVisible));
    if (ghostPaddleRef.current) ghostPaddleRef.current.visible = isVisible;

    if (!isVisible) return;

    // Generate ideal Pro Pose for current mode
    const proLandmarks = generateProPose(mode, animTimeRef.current, isRightHanded);
    const proPoints3D = computePosePoints3D(proLandmarks, undefined, true);

    for (let i = 0; i < 33; i++) {
      const v = proPoints3D[i];
      const gMesh = ghostJointMeshesRef.current[i];
      if (gMesh && v) {
        gMesh.position.copy(v);
      }
    }

    // Orient ghost bones
    for (let b = 0; b < BONE_DEFINITIONS.length; b++) {
      const [idxA, idxB] = BONE_DEFINITIONS[b].indices;
      const pA = proPoints3D[idxA];
      const pB = proPoints3D[idxB];
      const gBone = ghostBoneMeshesRef.current[b];
      if (gBone && pA && pB) {
        const mid = new THREE.Vector3().addVectors(pA, pB).multiplyScalar(0.5);
        gBone.position.copy(mid);
        const dir = new THREE.Vector3().subVectors(pB, pA);
        gBone.scale.set(1, Math.max(0.01, dir.length()), 1);
        dir.normalize();
        const up = new THREE.Vector3(0, 1, 0);
        gBone.setRotationFromQuaternion(new THREE.Quaternion().setFromUnitVectors(up, dir));
      }
    }

    // Ghost paddle
    const domWristIdx = isRightHanded ? POSE_LANDMARKS.RIGHT_WRIST : POSE_LANDMARKS.LEFT_WRIST;
    const domElbowIdx = isRightHanded ? POSE_LANDMARKS.RIGHT_ELBOW : POSE_LANDMARKS.LEFT_ELBOW;
    if (ghostPaddleRef.current && proPoints3D[domWristIdx] && proPoints3D[domElbowIdx]) {
      ghostPaddleRef.current.position.copy(proPoints3D[domWristIdx]);
      const dir = new THREE.Vector3()
        .subVectors(proPoints3D[domWristIdx], proPoints3D[domElbowIdx])
        .normalize();
      ghostPaddleRef.current.setRotationFromQuaternion(
        new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir)
      );
    }
  }, [showGhostPro, mode, isRightHanded, landmarks]);

  if (webglError) {
    return (
      <div className="relative w-full h-full min-h-[380px] bg-slate-950 rounded-2xl overflow-hidden border border-slate-800 shadow-2xl flex flex-col p-6 items-center justify-center text-center">
        <div className="w-16 h-16 rounded-2xl bg-sky-500/10 border border-sky-500/20 flex items-center justify-center mb-3 text-sky-400 shadow-inner">
          <RefreshCw className="w-8 h-8" />
        </div>
        <h3 className="text-base font-bold text-white mb-1.5">
          Không Gian Sân 3D Tạm Thời Giải Phóng Đồ Họa
        </h3>
        <p className="text-xs text-slate-400 max-w-sm mb-4 leading-relaxed">
          {webglError}
        </p>
        <button
          id="retry-webgl-btn"
          onClick={() => {
            setWebglError(null);
            setRetryCount(c => c + 1);
          }}
          className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-sky-500 to-emerald-500 hover:from-sky-400 hover:to-emerald-400 text-white font-semibold text-xs rounded-xl shadow-lg transition-all active:scale-95 cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" />
          Khởi Động Lại 3D Court
        </button>

        {/* Live Fallback Status Indicator */}
        <div className="mt-5 p-3 rounded-xl bg-slate-900/80 border border-slate-800 max-w-sm w-full text-left flex items-center justify-between text-xs shadow-md">
          <div>
            <div className="text-slate-400 text-[11px]">Hệ thống AI & Camera 2D:</div>
            <div className="text-emerald-400 font-medium flex items-center gap-1.5 mt-0.5 text-xs">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              Đang hoạt động & phân tích liên tục
            </div>
          </div>
          <div className="text-right">
            <div className="text-slate-400 text-[11px]">Điểm kỹ thuật:</div>
            <div className="text-sky-400 font-bold font-mono text-sm">{analysis.score}/100</div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative w-full h-full min-h-[380px] bg-slate-950 rounded-2xl overflow-hidden border border-slate-800 shadow-2xl flex flex-col">
      {/* 3D WebGL Canvas Container */}
      <div
        ref={mountRef}
        className="w-full h-full cursor-grab active:cursor-grabbing select-none touch-none flex-1"
      />

      {/* Top Floating Controls Bar */}
      <div className="absolute top-3 left-3 right-3 flex items-center justify-between pointer-events-none gap-2">
        {/* Real-time 3D Model Badge */}
        <div className="pointer-events-auto flex items-center gap-2 bg-slate-900/80 backdrop-blur-md px-3 py-1.5 rounded-full border border-slate-700/60 text-xs text-slate-200 shadow-lg">
          <span className="relative flex h-2.5 w-2.5">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
          </span>
          <span className="font-semibold tracking-wide">3D ATHLETE RIG (ĐỒNG BỘ 1:1)</span>
          <span className="text-slate-400">|</span>
          <span className="text-cyan-400 font-mono">
            {isRightHanded ? 'Tay Phải' : 'Tay Trái'}
          </span>
        </div>

        {/* Ghost Pro Player Overlay Toggle */}
        <button
          id="toggle-ghost-pro-btn"
          onClick={onToggleGhostPro}
          className={`pointer-events-auto flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold transition-all shadow-md border ${
            showGhostPro
              ? 'bg-amber-500/20 text-amber-300 border-amber-500/50 shadow-amber-500/10'
              : 'bg-slate-900/80 text-slate-300 border-slate-700/60 hover:bg-slate-800'
          }`}
          title="Bật/Tắt bóng mờ hình mẫu Pro (Ghost Ideal Form)"
        >
          <Sparkles className="w-3.5 h-3.5 text-amber-400" />
          <span>Form Mẫu Pro (Ghost)</span>
        </button>
      </div>

      {/* Bottom Camera View Presets */}
      <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between pointer-events-none">
        <div className="pointer-events-auto flex items-center gap-1 bg-slate-900/85 backdrop-blur-md p-1 rounded-xl border border-slate-700/60 shadow-lg">
          <span className="text-[11px] font-medium text-slate-400 px-2 flex items-center gap-1">
            <Camera className="w-3 h-3 text-slate-300" /> Góc nhìn:
          </span>
          <button
            id="view-slash-btn"
            onClick={() => handleViewSelect('slash')}
            className={`px-2.5 py-1 text-xs rounded-lg font-medium transition-all ${
              cameraView === 'slash'
                ? 'bg-sky-500 text-white shadow-sm'
                : 'text-slate-300 hover:text-white hover:bg-slate-800'
            }`}
          >
            Chéo 45°
          </button>
          <button
            id="view-front-btn"
            onClick={() => handleViewSelect('front')}
            className={`px-2.5 py-1 text-xs rounded-lg font-medium transition-all ${
              cameraView === 'front'
                ? 'bg-sky-500 text-white shadow-sm'
                : 'text-slate-300 hover:text-white hover:bg-slate-800'
            }`}
          >
            Chính diện
          </button>
          <button
            id="view-side-btn"
            onClick={() => handleViewSelect('side')}
            className={`px-2.5 py-1 text-xs rounded-lg font-medium transition-all ${
              cameraView === 'side'
                ? 'bg-sky-500 text-white shadow-sm'
                : 'text-slate-300 hover:text-white hover:bg-slate-800'
            }`}
          >
            Bên hông
          </button>
          <button
            id="view-kitchen-btn"
            onClick={() => handleViewSelect('kitchen')}
            className={`px-2.5 py-1 text-xs rounded-lg font-medium transition-all ${
              cameraView === 'kitchen'
                ? 'bg-emerald-500 text-white shadow-sm'
                : 'text-slate-300 hover:text-white hover:bg-slate-800'
            }`}
          >
            Vạch Bếp
          </button>
        </div>

        {/* Orbit Drag Tip */}
        <div className="hidden sm:flex items-center gap-1 text-[11px] text-slate-400 bg-slate-900/80 backdrop-blur-md px-2.5 py-1 rounded-lg border border-slate-800">
          <Move3d className="w-3 h-3 text-sky-400" />
          <span>Kéo chuột xoay 360°</span>
        </div>
      </div>
    </div>
  );
};
