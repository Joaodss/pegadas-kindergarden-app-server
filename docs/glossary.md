# Glossary (PT → EN)

The product documents are in Portuguese. Code, database objects and API routes are in English (ADR-0004). Use this table when translating a term from the product docs into code, and add to it before you introduce a new domain term.

| Portuguese (product docs) | English (code · API · DB) | Notes |
|---|---|---|
| Escola | School | The tenant. Every business table has `school_id` |
| Sala | Room | |
| Criança | Child | |
| Educador/a | Educator | Role of a `StaffMember` |
| Coordenação | Coordinator | Role of a `StaffMember` |
| Encarregado/a de educação | Guardian | Future (parents' portal) |
| Registo | DiaryEntry | Table `diary.entry`. The name avoids a clash with the C# `record` keyword |
| Tipo de registo | EntryType | Defines the measure: `none`, `count`, `volume_ml`, `duration`, `scale`, `text` |
| Barra de registo rápido | Quick bar (`room_entry_type`) | Entry types visible in a room, and their order |
| Registo em grupo | Group entry | N entries sharing a `group_id`, one per child |
| Sesta | Nap | An entry with `ended_at IS NULL` while ongoing |
| Presença · Chegada · Saída | Attendance · Arrival · Departure | Arrival and departure are system entry types |
| Dia da sala | RoomDay | Table `diary.room_day` |
| Fechar o dia | CloseDay | Emits the `DayClosed` domain event |
| Lacunas | Gaps | Missing expected entries in a day summary |
| Resumo (do dia, período) | Summary | |
| Histórico | History | |
| Ficha (alergias, restrições, notas) | ChildHealthProfile | GDPR Art. 9 data; every read is audited |
| Dispositivo (tablet da sala) | Device | Enrolled to one room |
| PIN do educador | PIN | Identifies the author on a shared device |
| Auditoria | Audit | `audit.audit_event`, append-only |
| Notificação | Notification | Future: push and email |
| Portal dos pais | Parents' portal | Future: `/v1/portal/...` |
