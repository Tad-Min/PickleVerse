/**
 * Types for Pickleball 3D Body Tracking & Real-Time Technique Analysis
 */

export interface Landmark3D {
  x: number;
  y: number;
  z: number;
  visibility?: number;
}

export type StrokeMode = 
  | 'FOREHAND_DRIVE'
  | 'DINK_KITCHEN'
  | 'LEGAL_SERVE'
  | 'OVERHEAD_SMASH'
  | 'FOOTWORK_NVZ';

export interface TechniqueMetric {
  name: string;
  currentValue: number;
  targetRange: [number, number];
  unit: string;
  status: 'optimal' | 'warning' | 'error';
  label: string;
}

export interface TechniqueError {
  id: string;
  joint: 'knee' | 'elbow' | 'wrist' | 'shoulder' | 'hip' | 'footwork' | 'paddle';
  title: string;
  description: string;
  severity: 'critical' | 'warning';
  timestamp: number;
}

export interface PoseAnalysisResult {
  score: number; // 0 to 100
  status: 'optimal' | 'warning' | 'error' | 'idle';
  statusMessage: string;
  metrics: TechniqueMetric[];
  errors: TechniqueError[];
  kneeFlexionAngle: number;
  elbowAngle: number;
  wristAngle: number;
  torsoRotation: number;
  swingSpeedEstimate: number;
  isKitchenViolated: boolean;
  isLegalServe: boolean;
}

export interface PoseTrackerStats {
  fps: number;
  latencyMs: number;
  isTracking: boolean;
  detectedLandmarksCount: number;
}

export interface DrillStats {
  totalSwings: number;
  perfectSwings: number;
  warningSwings: number;
  errorSwings: number;
  commonErrors: Record<string, number>;
  sessionDurationSec: number;
}
