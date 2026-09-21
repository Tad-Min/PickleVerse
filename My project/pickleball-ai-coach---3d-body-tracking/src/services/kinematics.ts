/**
 * Biomechanical kinematics calculations & Pickleball stroke error detectors
 */

import { Landmark3D, PoseAnalysisResult, StrokeMode, TechniqueError, TechniqueMetric } from '../types';

// Standard MediaPipe Pose Landmark Indices
export const POSE_LANDMARKS = {
  NOSE: 0,
  LEFT_EYE: 2,
  RIGHT_EYE: 5,
  LEFT_EAR: 7,
  RIGHT_EAR: 8,
  LEFT_SHOULDER: 11,
  RIGHT_SHOULDER: 12,
  LEFT_ELBOW: 13,
  RIGHT_ELBOW: 14,
  LEFT_WRIST: 15,
  RIGHT_WRIST: 16,
  LEFT_PINKY: 17,
  RIGHT_PINKY: 18,
  LEFT_INDEX: 19,
  RIGHT_INDEX: 20,
  LEFT_THUMB: 21,
  RIGHT_THUMB: 22,
  LEFT_HIP: 23,
  RIGHT_HIP: 24,
  LEFT_KNEE: 25,
  RIGHT_KNEE: 26,
  LEFT_ANKLE: 27,
  RIGHT_ANKLE: 28,
  LEFT_HEEL: 29,
  RIGHT_HEEL: 30,
  LEFT_FOOT_INDEX: 31,
  RIGHT_FOOT_INDEX: 32,
};

/**
 * Calculates angle in degrees between 3 points in 3D: A -> B (vertex) -> C
 */
export function calculateAngle3D(a: Landmark3D, b: Landmark3D, c: Landmark3D): number {
  const v1 = { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
  const v2 = { x: c.x - b.x, y: c.y - b.y, z: c.z - b.z };

  const dot = v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
  const mag1 = Math.sqrt(v1.x * v1.x + v1.y * v1.y + v1.z * v1.z);
  const mag2 = Math.sqrt(v2.x * v2.x + v2.y * v2.y + v2.z * v2.z);

  if (mag1 * mag2 === 0) return 0;
  let cosAngle = dot / (mag1 * mag2);
  cosAngle = Math.max(-1, Math.min(1, cosAngle));
  return (Math.acos(cosAngle) * 180) / Math.PI;
}

/**
 * Calculates Euclidean distance between 2 points in 3D
 */
export function distance3D(a: Landmark3D, b: Landmark3D): number {
  const dx = a.x - b.x;
  const dy = a.y - b.y;
  const dz = a.z - b.z;
  return Math.sqrt(dx * dx + dy * dy + dz * dz);
}

/**
 * Analyzes pose landmarks based on the selected Pickleball technique
 */
export function analyzePickleballPose(
  landmarks: Landmark3D[],
  mode: StrokeMode,
  isRightHanded = true,
  wristVelocityY = 0
): PoseAnalysisResult {
  if (!landmarks || landmarks.length < 33) {
    return {
      score: 0,
      status: 'idle',
      statusMessage: 'Đang tìm kiếm cơ thể trong khung hình...',
      metrics: [],
      errors: [],
      kneeFlexionAngle: 180,
      elbowAngle: 180,
      wristAngle: 0,
      torsoRotation: 0,
      swingSpeedEstimate: 0,
      isKitchenViolated: false,
      isLegalServe: true,
    };
  }

  const p = POSE_LANDMARKS;
  const domShoulder = isRightHanded ? landmarks[p.RIGHT_SHOULDER] : landmarks[p.LEFT_SHOULDER];
  const domElbow = isRightHanded ? landmarks[p.RIGHT_ELBOW] : landmarks[p.LEFT_ELBOW];
  const domWrist = isRightHanded ? landmarks[p.RIGHT_WRIST] : landmarks[p.LEFT_WRIST];
  const domIndex = isRightHanded ? landmarks[p.RIGHT_INDEX] : landmarks[p.LEFT_INDEX];
  const domHip = isRightHanded ? landmarks[p.RIGHT_HIP] : landmarks[p.LEFT_HIP];
  const domKnee = isRightHanded ? landmarks[p.RIGHT_KNEE] : landmarks[p.LEFT_KNEE];
  const domAnkle = isRightHanded ? landmarks[p.RIGHT_ANKLE] : landmarks[p.LEFT_ANKLE];

  const nonDomShoulder = isRightHanded ? landmarks[p.LEFT_SHOULDER] : landmarks[p.RIGHT_SHOULDER];
  const nonDomKnee = isRightHanded ? landmarks[p.LEFT_KNEE] : landmarks[p.RIGHT_KNEE];
  const nonDomHip = isRightHanded ? landmarks[p.LEFT_HIP] : landmarks[p.RIGHT_HIP];
  const nonDomAnkle = isRightHanded ? landmarks[p.LEFT_ANKLE] : landmarks[p.RIGHT_ANKLE];

  // Knee flexion (dominant & average)
  const domKneeAngle = calculateAngle3D(domHip, domKnee, domAnkle);
  const nonDomKneeAngle = calculateAngle3D(nonDomHip, nonDomKnee, nonDomAnkle);
  const avgKneeAngle = Math.round((domKneeAngle + nonDomKneeAngle) / 2);

  // Dominant Arm Elbow Angle
  const elbowAngle = Math.round(calculateAngle3D(domShoulder, domElbow, domWrist));

  // Wrist angle / Paddle angle relative to forearm
  const wristAngle = Math.round(calculateAngle3D(domElbow, domWrist, domIndex));

  // Torso / Shoulder line angle relative to camera plane
  const shoulderDx = nonDomShoulder.x - domShoulder.x;
  const shoulderDz = nonDomShoulder.z - domShoulder.z;
  const torsoRotation = Math.round(Math.abs((Math.atan2(shoulderDz, shoulderDx) * 180) / Math.PI));

  // Midpoint hip & navel height approximation
  const midHipY = (domHip.y + nonDomHip.y) / 2;

  const errors: TechniqueError[] = [];
  const metrics: TechniqueMetric[] = [];
  let score = 100;
  let isLegalServe = true;
  let isKitchenViolated = false;

  // Stance width relative to shoulder width
  const shoulderWidth = distance3D(domShoulder, nonDomShoulder);
  const feetWidth = distance3D(domAnkle, nonDomAnkle);
  const stanceRatio = feetWidth / (shoulderWidth || 0.001);

  // 1. KNEE & BASE CHECK (Universal for Pickleball)
  const kneeMetric: TechniqueMetric = {
    name: 'Độ khuỵu gối',
    currentValue: avgKneeAngle,
    targetRange: [125, 155],
    unit: '°',
    status: 'optimal',
    label: `${avgKneeAngle}° (Chuẩn: 125°-155°)`,
  };

  if (avgKneeAngle > 165) {
    kneeMetric.status = 'error';
    score -= 22;
    errors.push({
      id: 'knee-too-straight',
      joint: 'knee',
      title: 'Đầu gối quá thẳng',
      description: 'Hãy khuỵu gối xuống (130°-145°) để hạ thấp trọng tâm và giữ thăng bằng tối ưu.',
      severity: 'critical',
      timestamp: Date.now(),
    });
  } else if (avgKneeAngle > 155) {
    kneeMetric.status = 'warning';
    score -= 10;
    errors.push({
      id: 'knee-slightly-straight',
      joint: 'knee',
      title: 'Trọng tâm còn cao',
      description: 'Nên hạ gối sâu hơn một chút khi chuẩn bị đánh bóng.',
      severity: 'warning',
      timestamp: Date.now(),
    });
  }
  metrics.push(kneeMetric);

  // 2. STROKE SPECIFIC CHECKS
  if (mode === 'FOREHAND_DRIVE') {
    // Elbow metric
    const elbowMetric: TechniqueMetric = {
      name: 'Góc khuỷu tay vung',
      currentValue: elbowAngle,
      targetRange: [110, 160],
      unit: '°',
      status: elbowAngle >= 110 && elbowAngle <= 160 ? 'optimal' : 'warning',
      label: `${elbowAngle}° (Chuẩn: 110°-160°)`,
    };
    if (elbowAngle < 90) {
      elbowMetric.status = 'error';
      score -= 18;
      errors.push({
        id: 'elbow-cramped',
        joint: 'elbow',
        title: 'Khuỷu tay quá bó hẹp',
        description: 'Mở rộng tay hơn một chút và đón bóng ở phía trước thân mình.',
        severity: 'warning',
        timestamp: Date.now(),
      });
    } else if (elbowAngle > 170) {
      elbowMetric.status = 'warning';
      score -= 10;
      errors.push({
        id: 'elbow-hyperextended',
        joint: 'elbow',
        title: 'Cánh tay quá cứng/thẳng',
        description: 'Giữ khuỷu tay hơi chùng tự nhiên để tạo lực đòn bẩy từ vai và hông.',
        severity: 'warning',
        timestamp: Date.now(),
      });
    }
    metrics.push(elbowMetric);

    // Torso rotation metric
    metrics.push({
      name: 'Độ mở vai & hông',
      currentValue: torsoRotation,
      targetRange: [15, 60],
      unit: '°',
      status: torsoRotation >= 15 ? 'optimal' : 'warning',
      label: `${torsoRotation}°`,
    });

    if (torsoRotation < 12) {
      score -= 15;
      errors.push({
        id: 'lack-torso-rotation',
        joint: 'hip',
        title: 'Thiếu xoay vai và hông',
        description: 'Đừng chỉ dùng cổ tay/cánh tay! Hãy xoay vai để truyền lực động học (Kinetic Chain).',
        severity: 'warning',
        timestamp: Date.now(),
      });
    }

    // Low to high swing trajectory check:
    // In video frame coordinates, Y=0 is top, Y=1 is bottom.
    // Low to high means wrist Y decreases over time (velocityY < -0.05)
    if (wristVelocityY > 0.15) {
      score -= 15;
      errors.push({
        id: 'chopping-down',
        joint: 'paddle',
        title: 'Đang vung bổ từ trên xuống',
        description: 'Vung bóng pickleball drive cần đường bóng Low-to-High (từ dưới vung lên qua vai đối diện).',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }

  } else if (mode === 'DINK_KITCHEN') {
    // Dinking requires soft, locked wrist, paddle out in front, low athletic base
    const wristFlickMetric: TechniqueMetric = {
      name: 'Độ cố định cổ tay',
      currentValue: wristAngle,
      targetRange: [150, 180],
      unit: '°',
      status: wristAngle >= 145 ? 'optimal' : 'error',
      label: `${wristAngle}° (Khóa chắc chắn)`,
    };
    if (wristAngle < 140) {
      score -= 25;
      errors.push({
        id: 'wrist-flick',
        joint: 'wrist',
        title: 'Lỗi vẩy cổ tay khi Dink',
        description: 'Khóa chặt cổ tay! Dink phải được điều khiển bằng chuyển động con lắc từ khớp vai.',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }
    metrics.push(wristFlickMetric);

    // Paddle height relative to chest
    const paddleHeight = domWrist.y < midHipY;
    metrics.push({
      name: 'Vợt trước ngực sẵn sàng',
      currentValue: paddleHeight ? 100 : 40,
      targetRange: [80, 100],
      unit: '%',
      status: paddleHeight ? 'optimal' : 'warning',
      label: paddleHeight ? 'Sẵn sàng' : 'Hạ vợt quá thấp',
    });

    if (!paddleHeight) {
      score -= 18;
      errors.push({
        id: 'paddle-dropped',
        joint: 'paddle',
        title: 'Đầu vợt bị thõng xuống đất',
        description: 'Giữ đầu vợt luôn hướng lên phía trước ngực ở vạch bếp (NVZ) để phản xạ nhanh nhất.',
        severity: 'warning',
        timestamp: Date.now(),
      });
    }

  } else if (mode === 'LEGAL_SERVE') {
    // USA Pickleball Rule 4.A.5: Paddle head MUST be below the highest part of the wrist at impact
    // In standard coordinate: Y increases downwards. So domIndex.y > domWrist.y means index is lower than wrist (paddle down)
    const isPaddleHeadBelowWrist = domIndex.y >= domWrist.y - 0.04;
    const isContactBelowNavel = domWrist.y >= midHipY - 0.05;

    metrics.push({
      name: 'Đầu vợt dưới cổ tay (Luật 4.A)',
      currentValue: isPaddleHeadBelowWrist ? 100 : 0,
      targetRange: [100, 100],
      unit: '%',
      status: isPaddleHeadBelowWrist ? 'optimal' : 'error',
      label: isPaddleHeadBelowWrist ? 'Hợp lệ (Đầu vợt dốc xuống)' : 'PHẠM LUẬT (Đầu vợt quá cao)',
    });

    metrics.push({
      name: 'Điểm tiếp xúc dưới rốn',
      currentValue: isContactBelowNavel ? 100 : 0,
      targetRange: [100, 100],
      unit: '%',
      status: isContactBelowNavel ? 'optimal' : 'error',
      label: isContactBelowNavel ? 'Hợp lệ (Dưới thắt lưng)' : 'PHẠM LUẬT (Tiếp xúc trên eo)',
    });

    if (!isPaddleHeadBelowWrist) {
      isLegalServe = false;
      score -= 35;
      errors.push({
        id: 'illegal-serve-paddle-high',
        joint: 'wrist',
        title: 'Lỗi giao bóng: Đầu vợt cao hơn cổ tay',
        description: 'Luật Pickleball quy định khi giao bóng underhand, đỉnh đầu vợt phải nằm dưới phần cao nhất của cổ tay!',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }

    if (!isContactBelowNavel) {
      isLegalServe = false;
      score -= 30;
      errors.push({
        id: 'illegal-serve-high-contact',
        joint: 'paddle',
        title: 'Lỗi giao bóng: Điểm chạm bóng cao hơn rốn',
        description: 'Tiếp xúc bóng phải hoàn toàn nằm dưới đường viền eo/rốn của bạn.',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }

  } else if (mode === 'OVERHEAD_SMASH') {
    // Full extension of hitting arm overhead
    const smashReachMetric: TechniqueMetric = {
      name: 'Độ với tay khi Smash',
      currentValue: elbowAngle,
      targetRange: [160, 180],
      unit: '°',
      status: elbowAngle >= 155 ? 'optimal' : 'error',
      label: `${elbowAngle}° (Yêu cầu >155°)`,
    };
    if (elbowAngle < 150) {
      score -= 30;
      errors.push({
        id: 'smash-bent-arm',
        joint: 'elbow',
        title: 'Khuỷu tay co khi Smash',
        description: 'Duỗi thẳng cánh tay hết mức ở điểm cao nhất (Trophy position) để đập bóng cắm dốc.',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }
    metrics.push(smashReachMetric);

  } else if (mode === 'FOOTWORK_NVZ') {
    // Check stance width for balanced split step
    const footworkMetric: TechniqueMetric = {
      name: 'Độ rộng bước chân',
      currentValue: Math.round(stanceRatio * 100),
      targetRange: [110, 160],
      unit: '%',
      status: stanceRatio >= 1.1 && stanceRatio <= 1.6 ? 'optimal' : 'warning',
      label: stanceRatio >= 1.1 ? 'Vững chãi' : 'Chân quá hẹp',
    };
    if (stanceRatio < 1.0) {
      score -= 20;
      errors.push({
        id: 'narrow-stance',
        joint: 'footwork',
        title: 'Chân đứng quá hẹp',
        description: 'Mở rộng 2 chân bằng hoặc hơn vai một chút (120%) để phản xạ di chuyển ngang tức thì.',
        severity: 'warning',
        timestamp: Date.now(),
      });
    }
    metrics.push(footworkMetric);

    // Kitchen line foot crossing check (Z-depth or simulated net proximity)
    const forwardFootZ = Math.min(domAnkle.z, nonDomAnkle.z);
    if (forwardFootZ < -0.35) {
      isKitchenViolated = true;
      score -= 40;
      errors.push({
        id: 'kitchen-fault',
        joint: 'footwork',
        title: 'LỖI PHẠM VẠCH BẾP (KITCHEN FAULT)',
        description: 'Bàn chân giẫm/vượt qua vạch Non-Volley Zone (Bếp) khi thực hiện cú đánh!',
        severity: 'critical',
        timestamp: Date.now(),
      });
    }
  }

  score = Math.max(10, Math.min(100, score));

  let status: 'optimal' | 'warning' | 'error' = 'optimal';
  let statusMessage = 'Kỹ thuật rất tốt! Tiếp tục duy trì phong độ.';

  if (score < 60 || errors.some(e => e.severity === 'critical')) {
    status = 'error';
    statusMessage = errors[0]?.title || 'Cần sửa lỗi kỹ thuật!';
  } else if (score < 85 || errors.length > 0) {
    status = 'warning';
    statusMessage = errors[0]?.title || 'Tư thế chưa tối ưu, cần điều chỉnh nhỏ.';
  }

  return {
    score,
    status,
    statusMessage,
    metrics,
    errors,
    kneeFlexionAngle: avgKneeAngle,
    elbowAngle,
    wristAngle,
    torsoRotation,
    swingSpeedEstimate: Math.abs(wristVelocityY) * 100,
    isKitchenViolated,
    isLegalServe,
  };
}
