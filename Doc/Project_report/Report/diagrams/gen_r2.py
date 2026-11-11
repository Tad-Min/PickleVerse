"""PlantUML sources for Report 2: development process and schedule Gantt chart."""
import os
from diagram_lib import write_all, SRC

D = {}

D["r2_process"] = r"""
skinparam ActivityBackgroundColor #F5F9FC
title Two-week iteration
start
:Product backlog\n(features FE-01 to FE-10,\ndefects, technical items);
repeat
  :Iteration planning\n(select items, estimate, assign owner);
  fork
    :Design and update\nSRS / SDD;
  fork again
    :Implement in a branch\n(Unity, API, AI service, admin web);
  fork again
    :Write unit and\nintegration tests;
  end fork
  :Pull request review\nand CI validate job;
  if (Definition of Done met?) then (yes)
    :Merge into main;\nCI deploys to staging;
  else (no)
    :Rework the item;
  endif
  :Iteration review with demo\n(supervisor at review weeks);
  :Retrospective;\nupdate tracking workbook;
repeat while (Remaining backlog\nfor the release?) is (yes)
->no;
:Release 1.0 and final defense;
stop
"""

GANTT = r"""@startgantt r2_gantt
!include _style.iuml
<style>
ganttDiagram {
  task { FontSize 15 }
  milestone { FontSize 15 }
  timeline { FontSize 14 }
  separator { FontSize 15 }
}
</style>
printscale weekly zoom 1
Project starts 2026-09-07
-- Iterations --
[It-0 Inception] starts 2026-09-07 and ends 2026-09-18
[It-1 Prototype] starts 2026-09-21 and ends 2026-10-02
[It-2 Single-player core] starts 2026-10-05 and ends 2026-10-16
[It-3 Online and backend] starts 2026-10-19 and ends 2026-10-30
[It-4 Dedicated server and coaching] starts 2026-11-02 and ends 2026-11-13
[It-5 Stabilisation] starts 2026-11-16 and ends 2026-11-27
[It-6 Release] starts 2026-11-30 and ends 2026-12-11
[It-0 Inception] is 100% completed
[It-1 Prototype] is 100% completed
[It-2 Single-player core] is 100% completed
[It-3 Online and backend] is 100% completed
[It-4 Dedicated server and coaching] is 70% completed
-- Milestones (due dates) --
[M01 Report 1] happens 2026-09-13
[M02 Report 2] happens 2026-09-20
[M03 Report 3] happens 2026-09-27
[M04 Report 4] happens 2026-10-11
[M05 Package 1 / Review 2] happens 2026-10-25
[M06 Package 2] happens 2026-11-08
[M07 Package 3 / Review 3] happens 2026-11-22
[M08 Full package] happens 2026-12-06
[M09 Report 6] happens 2026-12-13
[M10 Final defense] happens 2026-12-20
today is 2026-11-11 and is colored in #FFE0E0
@endgantt
"""

if __name__ == "__main__":
    write_all(D)
    with open(os.path.join(SRC, "r2_gantt.puml"), "w", encoding="utf-8") as f:
        f.write(GANTT)
    print("ok")
