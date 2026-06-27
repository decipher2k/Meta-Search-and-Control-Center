# Project Aurora Risk Register

| Risk ID | Area | Description | Owner | Mitigation | Status |
| --- | --- | --- | --- | --- | --- |
| AUR-R1 | Privacy | Hamburg sensor corridor may include school-zone movement patterns. | Lena Vogt | Run Civic Data Trust review before field activation. | Open |
| AUR-R2 | Data Quality | Munich retail demand feed has weekend gaps. | Omar Klein | Add synthetic backfill and merchant validation sample. | Monitoring |
| AUR-R3 | Infrastructure | Cologne energy telemetry arrives in 30-minute batches. | Priya Raman | Cache batch windows and mark stale forecasts. | In Progress |
| AUR-R4 | Governance | Berlin has overlapping approvals from transport, retail and citizen boards. | Mara Stein | Prepare single launch review pack with clear evidence. | Open |

The launch review should not approve Hamburg until AUR-R1 is closed. The
highest business upside is still Munich, but privacy readiness is better in
Cologne.
