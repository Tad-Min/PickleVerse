/**
 * Synthetic Motion Generator for Pro Pickleball Techniques & Demonstrations
 * Generates smooth 33-landmark pose streams for testing, drill demonstrations, and comparisons.
 */

import { Landmark3D, StrokeMode } from '../types';

export function generateProPose(mode: StrokeMode, tSec: number, isRightHanded = true): Landmark3D[] {
  const landmarks: Landmark3D[] = [];
  // Base neutral 33 landmarks
  for (let i = 0; i < 33; i++) {
    landmarks.push({ x: 0.5, y: 0.5, z: 0, visibility: 0.99 });
  }

  // Animation cycle phase (0 to 1)
  const cycle = (tSec % 2.5) / 2.5;
  const sinProgress = Math.sin(cycle * Math.PI * 2);
  const cosProgress = Math.cos(cycle * Math.PI * 2);

  // Default Head and Torso
  landmarks[0] = { x: 0.5, y: 0.22, z: 0.0 }; // Nose
  landmarks[11] = { x: 0.42, y: 0.32, z: 0.02 }; // Left shoulder
  landmarks[12] = { x: 0.58, y: 0.32, z: -0.02 }; // Right shoulder

  // Hips
  landmarks[23] = { x: 0.44, y: 0.56, z: 0.01 }; // Left hip
  landmarks[24] = { x: 0.56, y: 0.56, z: -0.01 }; // Right hip

  if (mode === 'FOREHAND_DRIVE') {
    // Athletic knee bend
    const kneeY = 0.73;
    const kneeBend = 0.04 * Math.sin(cycle * Math.PI * 2);
    landmarks[25] = { x: 0.42, y: kneeY + kneeBend, z: 0.08 }; // Left knee
    landmarks[26] = { x: 0.59, y: kneeY + kneeBend, z: -0.05 }; // Right knee
    landmarks[27] = { x: 0.40, y: 0.90, z: 0.02 }; // Left ankle
    landmarks[28] = { x: 0.62, y: 0.90, z: -0.02 }; // Right ankle

    // Swing kinetic chain: Backswing -> Contact -> Follow-through
    // Right arm
    if (cycle < 0.35) {
      // Backswing: paddle dropped low and back
      const p = cycle / 0.35;
      landmarks[14] = { x: 0.64 + 0.04 * p, y: 0.46 + 0.05 * p, z: -0.15 * p }; // Right elbow
      landmarks[16] = { x: 0.67 + 0.08 * p, y: 0.58 + 0.10 * p, z: -0.22 * p }; // Right wrist
      landmarks[20] = { x: 0.69 + 0.08 * p, y: 0.64 + 0.10 * p, z: -0.25 * p }; // Right index (paddle)
    } else if (cycle < 0.65) {
      // Forward swing & contact out in front (Low-to-High)
      const p = (cycle - 0.35) / 0.3;
      landmarks[14] = { x: 0.68 - 0.16 * p, y: 0.51 - 0.10 * p, z: -0.15 + 0.25 * p };
      landmarks[16] = { x: 0.75 - 0.28 * p, y: 0.68 - 0.28 * p, z: -0.22 + 0.38 * p }; // Wrist in front!
      landmarks[20] = { x: 0.77 - 0.30 * p, y: 0.74 - 0.32 * p, z: -0.25 + 0.40 * p };
    } else {
      // Follow-through across opposite shoulder
      const p = (cycle - 0.65) / 0.35;
      landmarks[14] = { x: 0.52 - 0.08 * p, y: 0.41 - 0.06 * p, z: 0.10 - 0.05 * p };
      landmarks[16] = { x: 0.47 - 0.10 * p, y: 0.40 - 0.08 * p, z: 0.16 - 0.10 * p }; // Over left shoulder
      landmarks[20] = { x: 0.47 - 0.10 * p, y: 0.32 - 0.08 * p, z: 0.15 - 0.10 * p };
    }

    // Left arm balance
    landmarks[13] = { x: 0.36, y: 0.42, z: 0.05 + 0.06 * sinProgress };
    landmarks[15] = { x: 0.34, y: 0.48, z: 0.08 + 0.08 * sinProgress };
    landmarks[19] = { x: 0.33, y: 0.50, z: 0.09 };

  } else if (mode === 'DINK_KITCHEN') {
    // Deep athletic stance, low center of gravity at NVZ line
    landmarks[23] = { x: 0.43, y: 0.59, z: 0.0 };
    landmarks[24] = { x: 0.57, y: 0.59, z: 0.0 };
    landmarks[25] = { x: 0.41, y: 0.76, z: 0.12 }; // Deep knee bend 135 deg
    landmarks[26] = { x: 0.59, y: 0.76, z: 0.12 };
    landmarks[27] = { x: 0.39, y: 0.91, z: 0.0 };
    landmarks[28] = { x: 0.61, y: 0.91, z: 0.0 };

    // Locked wrist, pendulum stroke from shoulder
    const dinkCycle = Math.sin(tSec * 3);
    landmarks[14] = { x: 0.59, y: 0.44 + 0.02 * dinkCycle, z: 0.12 };
    landmarks[16] = { x: 0.56, y: 0.46 + 0.05 * dinkCycle, z: 0.22 }; // Paddle in front of chest
    landmarks[20] = { x: 0.56, y: 0.40 + 0.05 * dinkCycle, z: 0.25 }; // Head pointing up!

    // Left hand in ready guard
    landmarks[13] = { x: 0.38, y: 0.44, z: 0.12 };
    landmarks[15] = { x: 0.42, y: 0.46, z: 0.20 };
    landmarks[19] = { x: 0.42, y: 0.42, z: 0.22 };

  } else if (mode === 'LEGAL_SERVE') {
    // Upward arc, paddle head below wrist, contact below navel
    const servePhase = (tSec % 2.0) / 2.0;
    const upwardY = 0.66 - 0.25 * Math.sin(servePhase * Math.PI);
    landmarks[14] = { x: 0.60, y: 0.48, z: 0.05 };
    landmarks[16] = { x: 0.58, y: upwardY, z: 0.15 };
    // Critical: index landmark lower in Y than wrist (paddle head down)
    landmarks[20] = { x: 0.58, y: upwardY + 0.08, z: 0.18 };

    landmarks[25] = { x: 0.43, y: 0.74, z: 0.05 };
    landmarks[26] = { x: 0.58, y: 0.74, z: -0.02 };
    landmarks[27] = { x: 0.42, y: 0.90, z: 0.0 };
    landmarks[28] = { x: 0.60, y: 0.90, z: -0.05 };

  } else if (mode === 'OVERHEAD_SMASH') {
    // High reach overhead extension
    const smashCycle = (tSec % 2.0) / 2.0;
    const reachY = smashCycle < 0.5 ? 0.15 - 0.08 * Math.sin(smashCycle * Math.PI * 2) : 0.38;
    landmarks[14] = { x: 0.60, y: 0.24, z: 0.08 };
    landmarks[16] = { x: 0.60, y: reachY, z: 0.14 }; // high reach above head!
    landmarks[20] = { x: 0.60, y: reachY - 0.10, z: 0.18 };

    landmarks[25] = { x: 0.43, y: 0.72, z: 0.02 };
    landmarks[26] = { x: 0.57, y: 0.70, z: 0.08 };
    landmarks[27] = { x: 0.42, y: 0.89, z: 0.0 };
    landmarks[28] = { x: 0.58, y: 0.88, z: 0.06 };

  } else {
    // FOOTWORK_NVZ: dynamic lateral shuffle at kitchen line
    const shuffleX = 0.08 * Math.sin(tSec * 4);
    landmarks[23].x += shuffleX;
    landmarks[24].x += shuffleX;
    landmarks[25].x += shuffleX;
    landmarks[26].x += shuffleX;
    landmarks[27].x += shuffleX;
    landmarks[28].x += shuffleX;
    landmarks[11].x += shuffleX;
    landmarks[12].x += shuffleX;
    landmarks[0].x += shuffleX;

    // Wide stance
    landmarks[25] = { x: 0.40 + shuffleX, y: 0.74, z: 0.05 };
    landmarks[26] = { x: 0.61 + shuffleX, y: 0.74, z: 0.05 };
    landmarks[27] = { x: 0.36 + shuffleX, y: 0.90, z: 0.0 };
    landmarks[28] = { x: 0.65 + shuffleX, y: 0.90, z: 0.0 };
  }

  // Synthesize articulated hand digits (wrists -> thumb, index, pinky)
  // Left Hand digits
  const lw = landmarks[15];
  landmarks[17] = { x: lw.x - 0.025, y: lw.y + 0.035, z: lw.z + 0.015, visibility: 0.96 }; // left pinky
  landmarks[19] = { x: lw.x - 0.005, y: lw.y + 0.045, z: lw.z + 0.025, visibility: 0.96 }; // left index
  landmarks[21] = { x: lw.x + 0.020, y: lw.y + 0.028, z: lw.z + 0.020, visibility: 0.96 }; // left thumb

  // Right Hand digits (accommodating swing/paddle)
  const rw = landmarks[16];
  landmarks[18] = { x: rw.x + 0.025, y: rw.y + 0.035, z: rw.z + 0.015, visibility: 0.96 }; // right pinky
  if (!landmarks[20] || landmarks[20].x === 0.5) {
    landmarks[20] = { x: rw.x + 0.005, y: rw.y + 0.045, z: rw.z + 0.025, visibility: 0.96 }; // right index
  }
  landmarks[22] = { x: rw.x - 0.020, y: rw.y + 0.028, z: rw.z + 0.020, visibility: 0.96 }; // right thumb

  // Synthesize articulated feet digits (ankles -> heel, foot index/toe)
  // Left Foot digits
  const la = landmarks[27];
  landmarks[29] = { x: la.x, y: la.y + 0.025, z: la.z - 0.045, visibility: 0.96 }; // left heel
  landmarks[31] = { x: la.x, y: la.y + 0.035, z: la.z + 0.065, visibility: 0.96 }; // left toe/index

  // Right Foot digits
  const ra = landmarks[28];
  landmarks[30] = { x: ra.x, y: ra.y + 0.025, z: ra.z - 0.045, visibility: 0.96 }; // right heel
  landmarks[32] = { x: ra.x, y: ra.y + 0.035, z: ra.z + 0.065, visibility: 0.96 }; // right toe/index

  // Handle Left-handed mirror if requested
  if (!isRightHanded) {
    return landmarks.map(lm => ({
      ...lm,
      x: 1.0 - lm.x,
      z: -lm.z,
    }));
  }

  return landmarks;
}
