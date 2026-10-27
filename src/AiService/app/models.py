from typing import Literal
from pydantic import BaseModel, ConfigDict, Field


class WireModel(BaseModel):
    model_config = ConfigDict(extra="forbid", allow_inf_nan=False)


class Shot(WireModel):
    shotId: str = Field(min_length=1, max_length=100)
    kind: str = Field(max_length=40)
    technique: str = Field(max_length=40)
    source: str = Field(default="", max_length=40)
    outcome: str = Field(default="unknown", max_length=40)
    sequence: int = Field(ge=0)
    rally: int = Field(default=0, ge=0)
    time: float = Field(default=0, ge=0)
    timingError: float | None = None
    contact: list[float] | None = Field(default=None, min_length=3, max_length=3)
    leftFoot: list[float] | None = Field(default=None, min_length=3, max_length=3)
    rightFoot: list[float] | None = Field(default=None, min_length=3, max_length=3)
    intendedTarget: list[float] | None = Field(default=None, min_length=3, max_length=3)
    landing: list[float] | None = Field(default=None, min_length=3, max_length=3)
    kitchen: bool | None = None
    volley: bool | None = None
    momentum: bool | None = None
    recovered: bool | None = None
    targetHit: bool | None = None
    bounces: int | None = Field(default=None, ge=0)
    netClearance: float | None = None
    engineAimed: bool = True
    assisted: bool = True


class Metric(WireModel):
    technique: str
    code: str
    opportunities: int = Field(ge=0)
    errors: int = Field(ge=0)
    severity: int


class Drill(WireModel):
    id: str
    title: str
    skill: str
    tier: int = Field(default=0, ge=0, le=3)
    prerequisites: list[str] = Field(default_factory=list)


class AnalyzeRequest(WireModel):
    sessionId: str
    ruleVersion: Literal["rules-v1"] = "rules-v1"
    mode: str
    shots: list[Shot] = Field(max_length=100000)
    onboardingShots: list[Shot] = Field(default_factory=list, max_length=3000)
    history: list[list[Metric]] = Field(default_factory=list, max_length=9)
    drills: list[Drill] = Field(default_factory=list)
    completedLessons: list[str] = Field(default_factory=list)
    fallbackLesson: str = "serve"
    resumeActive: bool = False
    # Disabled until calibrated game fixtures are shipped with a new rules version.
    dinkClearanceThreshold: float | None = None
