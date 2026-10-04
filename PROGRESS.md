# SASMS — Project Progress Handoff

**Project:** Secure Attendance and Shift Management System (SASMS)
**Type:** Final Year Project (FYP2 implementation) — Cybersecurity / Web Application
**Client (fictional/case study):** Subway Metro Point Complex
**Location on disk:** `C:\Users\fauza\Desktop\Degree\SASMS`

This file is a handoff/status summary of everything built and decided so far, written to be
pasted into another AI tool or shared with a collaborator without extra context. The
authoritative requirements baseline is `Secure Attendance and Shift Management System
(SASMS).md` in the project root (the FYP1 analysis/design document) — treat this file as
the current *implementation* status against that baseline, not a replacement for it.

---

## 1. What SASMS is

A web-based attendance and shift management system whose core security feature is
attendance verification via **facial recognition + basic liveness detection**, combined
with **Role-Based Access Control (RBAC)** separating two roles: **Employee** and
**Manager/PIC** (administrator). The problem it solves: buddy punching / weak attendance
verification, manual Excel-based scheduling, and lack of attendance reporting.

## 2. Technology stack (fixed, do not change without discussion)

| Component | Technology |
|---|---|
| Backend framework | ASP.NET Core MVC, .NET 8 |
| Database | MySQL (currently via Laragon's bundled MySQL 8.4.3) |
| ORM | EF Core 8 + Pomelo.EntityFrameworkCore.MySql |
| Facial verification / liveness | Python + OpenCV (matching) + MediaPipe (liveness) — **built, real-webcam tested, and physical-iPhone tested** (see §10, §11, §17) |
| Frontend | Razor views + Bootstrap 5 (via CDN), minimal custom JS, Subway-brand palette (see §5) |
| Auth | Custom lean cookie-based auth (see §4), **not** full ASP.NET Core Identity |

## 3. Current implementation status: **Backend phase — complete and verified working**

Everything below is implemented, builds with 0 errors/warnings, and has been smoke-tested
end-to-end against a live database (not just compiled — actually run, clicked through,
and inspected in the DB):

- **Authentication & RBAC** — login/logout, account lockout after repeated failed
  attempts, forced global CSRF protection, password change flow. Two roles: `Employee`
  (RoleId 1) and `Manager/PIC` (RoleId 2), enforced server-side via `[Authorize(Roles=...)]`
  on every controller/action — verified an Employee gets redirected to Access Denied when
  hitting Manager-only routes directly by URL.
- **Employee management** — CRUD by Manager/PIC only; "delete" is a soft delete
  (deactivate) that disables the linked login but preserves attendance/schedule history.
  Creating an employee also creates their login account and shows a one-time generated
  temporary password (never stored in plaintext, never emailed — handed over manually).
- **Shift scheduling** — Manager/PIC creates/edits/deletes shifts; Employees see their own
  schedule read-only. Employees get a notification on any change to their schedule.
- **Attendance** — Employee (and now Manager/PIC — see §10) check-in/check-out, gated on a
  live webcam capture that's run through the Python/OpenCV face-service. Verification
  happens *before* any record is created: an Attendance row is only ever written on a real
  success (`VerificationStatus = Verified`); a failed attempt leaves no row at all, just an
  audit entry — **this is the critical security gate the whole project is built around**
  (see §11 for why this changed from an earlier Pending-then-update design). Failed live
  attempts are capped at 3 per day per action (check-in/check-out tracked separately) before
  that action is blocked pending Manager/PIC review. A clearly-labelled Manager-only manual
  override still exists as a documented fallback for when verification can't be completed
  (service down, not yet registered, blocked after 3 failed attempts, etc.).
- **Facial verification integration** — two paths, deliberately different: (1) the
  interactive check-in/out/registration flow calls the Python face-service **in-process**
  from `AttendanceController` (browser → ASP.NET Core → Python, synchronous, immediate
  result); (2) `POST /api/verification/callback` in `Controllers/Api/VerificationController.cs`
  remains as a seam for a genuinely external/decoupled caller (shared secret in the
  `X-Verification-Secret` header against `VerificationService:SharedSecret`), not used by
  the interactive flow itself. See §10 for the full facial verification build.
- **Reports** — monthly attendance summary, late-attendance, absence, and overtime
  reports, each with CSV export, Manager/PIC only.
- **Notifications** — simple per-user notification list (schedule changes, marked-absent
  events, etc.), mark-as-read.
- **Audit log** — every sensitive action (logins, employee CRUD, schedule changes,
  attendance check-in/out, and especially the manual verification override) writes a row
  to `AuditLogs` with who/what/when/IP. Verified populated correctly during testing.

## 4. Key architecture decision: lean custom auth, not ASP.NET Core Identity

Deliberately **not** using the full ASP.NET Core Identity framework
(`UserManager`/`SignInManager`/`IdentityDbContext`). Instead: a custom `Users`/`Roles`
schema matching the FYP1 doc's own conceptual model (`Users`: UserId, Username,
PasswordHash, EmployeeId, RoleId, Status; `Roles`: RoleId, RoleName), cookie authentication
configured by hand, and ASP.NET Core's built-in `PasswordHasher<T>` (PBKDF2) for secure
hashing — that class ships in the ASP.NET Core shared framework, no extra NuGet package
needed.

**Why:** this is a university FYP that will be defended/evaluated; the student needs to be
able to explain the schema in a viva. Full Identity's ~7-table schema is overkill and
doesn't match the doc's simple conceptual model. This was an explicit, discussed decision,
not a default.

## 5. UI: Subway-brand redesign (built on top of the backend phase, purely visual)

The default Bootstrap look was reskinned to read as real client-branded software:

- **Design system** in `wwwroot/css/site.css` — green `#00543C`/yellow `#FFC600` palette,
  applied mostly via Bootstrap 5.3 CSS-variable overrides (`--bs-primary`,
  `--bs-success/warning/danger/info` + their `-bg-subtle`/`-text-emphasis` companions) so
  most components reskin from one place. Status colors (good/warning/serious/critical) are
  validated colorblind-safe steps, not invented.
- **Shared components** (reuse these instead of duplicating status-color logic):
  `Models/ViewModels/StatusBadgeExtensions.cs` + `Views/Shared/_StatusBadge.cshtml`
  (`.ToBadge()` on `AttendanceStatus`/`VerificationStatus`/`EmploymentStatus`);
  `ViewComponents/NotificationBellViewComponent.cs` +
  `Views/Shared/Components/NotificationBell/Default.cshtml` (live unread-count bell,
  available on every page via `@await Component.InvokeAsync("NotificationBell")`);
  `.stat-tile` CSS component for dashboard KPI cards.
- **Real logo**: the client's actual Subway logo, cropped/resized into
  `wwwroot/images/subway-logo.png` and `wwwroot/favicon.png`, used in the navbar and login
  page. Legitimate — this is internal software for that specific franchise location, not
  public redistribution of the trademark.
- **Login page** rebuilt as a single centered card with a soft green page backdrop
  (`Views/Account/Login.cshtml`, `body.login-page` styling).
- **Layout note:** a full sidebar-navigation layout was built and then explicitly reverted
  back to the original top navbar in the same session — only the notification bell was
  kept (moved into the navbar). Don't re-propose a sidebar without being asked again.

## 6. Project structure

```
SASMS/
  SASMS.sln
  Secure Attendance and Shift Management System (SASMS).md   <- FYP1 baseline doc
  PROGRESS.md                                                  <- this file
  database/schema.sql          <- manual phpMyAdmin fallback schema (EF migrations are the source of truth)
  face-service/                 <- Python/OpenCV + MediaPipe facial verification service (see §10, §17)
    app.py, face_engine.py, requirements.txt, .venv/ (gitignored), data/ (gitignored)
    models/face_landmarker.task   <- MediaPipe landmark model, one-time download, gitignored (see README)
    test_harness.html            <- dev-only webcam test page, served at GET /test
  src/SASMS.Web/
    dev-https-cert/              <- mkcert-generated local HTTPS cert, gitignored, machine-local (see §17.1)
    Program.cs                 <- app startup, DI, auth config, middleware pipeline, rate limiting, CSP
    appsettings.json            <- committed, empty connection string (placeholder)
    appsettings.Development.json <- local dev connection string + verification/face-service shared secrets (gitignored)
    Properties/launchSettings.json <- makes `dotnet run` use Development env automatically
    Models/Entities/            <- User, Role, Employee, Attendance, Schedule, Notification, AuditLog
    Models/Enums/                <- RoleType, UserStatus, EmploymentStatus, AttendanceStatus, VerificationStatus
    Models/ViewModels/           <- per-page form models with validation attributes
    Models/ApiDtos/              <- VerificationCallbackDto, FaceFramesSubmission
    Data/ApplicationDbContext.cs <- EF Core model config
    Data/DbSeeder.cs             <- seeds Roles + one Manager/PIC account on first run
    Security/                    <- ICurrentUserService, RoleNames, lockout/verification/face-service settings
    Services/                    <- business logic layer (one interface+impl per module), FaceVerificationClient
    ViewComponents/               <- NotificationBellViewComponent
    Controllers/                 <- Account, Home, Employees, Attendance, Schedules, Reports, Notifications, Api/Verification
    Views/                       <- Razor views, Bootstrap 5 via CDN, Subway-brand CSS
    wwwroot/js/                  <- site.js (confirm dialogs), face-capture.js (webcam burst capture)
```

## 7. Local dev environment (this specific machine)

- **.NET 8 SDK** installed via winget (`Microsoft.DotNet.SDK.8`, use `--source winget` to
  avoid an msstore agreement error).
- **`dotnet-ef` global tool** installed for migrations (`dotnet tool install --global
  dotnet-ef`); make sure `%USERPROFILE%\.dotnet\tools` is on PATH.
- **MySQL runs via Laragon** (`C:\laragon`, MySQL 8.4.3), not XAMPP. The XAMPP install on
  this machine (`C:\xampp`) turned out to be version 1.7.4 from ~2011 bundling MySQL 5.5.8
  and Apache 2.2.17 — years past end-of-life, and MySQL 5.5 doesn't even support the SQL
  EF Core generates (fractional-seconds `TIME` precision), which is what surfaced this in
  the first place. XAMPP had installed **both MySQL and Apache as auto-starting Windows
  services** (`mysql`, `Apache2.2`), which kept re-grabbing ports 3306/80 after every
  reboot even after Laragon was installed — both were stopped and set to Manual startup.
- Database name: `sasms_db`, created directly via `mysql -u root -e "CREATE DATABASE ..."`
  (connection string requires the DB to already exist since it's specified as the default
  schema at connect time).
- `dotnet run` from `src/SASMS.Web` now works with zero manual setup — it used to require
  manually setting `ASPNETCORE_ENVIRONMENT=Development` every session (otherwise it loads
  the empty connection string from `appsettings.json` instead of the real one in
  `appsettings.Development.json`, and crashes with a MySQL "host not allowed" error); this
  is now fixed permanently via `Properties/launchSettings.json`.
- A `warn: ... Failed to determine the https port for redirect` line on every request is
  expected/harmless when running HTTP-only — ignore it.
- **Python 3.12** installed via winget (`Python.Python.3.12`). Same PATH-shadowing issue as
  everything else on this machine: the Windows Store app-execution-alias stub for `python`
  wins over the real install unless you activate the `face-service/.venv` venv first
  (`.venv\Scripts\Activate.ps1`) — do that rather than fighting system PATH.
- Running the app now needs **three things up**: Laragon's MySQL, the face-service
  (`cd face-service; .venv\Scripts\Activate.ps1; $env:FACE_SERVICE_SHARED_SECRET="dev-only-face-service-secret-change-me"; python app.py`),
  then `dotnet run` as usual. See §10.

### Seeded/test accounts (local dev database only)
| Username | Password | Role |
|---|---|---|
| `admin` | `[REDACTED]` *(stale as of 2026-08-24 — login with this failed; likely changed via Change Password since)* | Manager/PIC (auto-seeded on first run) |
| `test.employee` | `[REDACTED]` *(also unverified as of 2026-08-24)* | Employee (created during testing, has a schedule + one check-in already recorded) |

These were one-time generated local dev passwords, not real secrets. There is deliberately
no public self-registration page; only a logged-in Manager/PIC can create new accounts. If
both are actually locked out, wipe the `Users`/`Employees` tables in `sasms_db` and restart
the app to reseed a fresh admin account (a new one-time password prints to the console).

## 8. How to run it

```powershell
cd src\SASMS.Web
dotnet run
```
Then open the URL it prints (defaults to `http://localhost:5080` and auto-opens the
browser). Make sure Laragon's MySQL is running first.

## 9. Security hardening pass (2026-09-01)

User asked for a compliance-style review against a broad checklist (encryption, hashing,
authN/authZ, RBAC, Redis, session/token mgmt, JWT/OAuth2, DB transactions, concurrency,
rate limiting, idempotency, API security, audit logging, message queue, retry/rollback,
load balancing, SQL injection, business rules). Assessment + fixes:

- **Already solid, no changes needed**: password hashing, RBAC, server-side authZ,
  SQL-injection prevention (100% EF Core LINQ), audit logging, CSRF.
- **Fixed — concurrency**: added a unique index on `Schedules(EmployeeId, ShiftDate)`
  (migration `AddScheduleUniqueIndex`) closing a double-booking race that Attendance's
  equivalent index already prevented. Services now catch the resulting duplicate-key error
  gracefully via `Services/DbConcurrencyHelper.cs` instead of a raw 500.
- **Fixed — retry & rollback**: `EnableRetryOnFailure()` on the DbContext. Required
  rewriting `EmployeeService.CreateAsync`'s manual transaction to go through
  `Database.CreateExecutionStrategy().ExecuteAsync(...)` — mixing a manual transaction with
  retry-on-failure without that wrapper throws at runtime on the first real retry.
- **Fixed — rate limiting**: built-in `Microsoft.AspNetCore.RateLimiting`, per-client-IP
  policies — `"login"` (10/min) on the login POST, `"verification-api"` (20/min) on the
  facial-verification callback (previously brute-forceable with no protection at all,
  unlike user login which has account lockout). Verified live: 429 kicks in after 10
  requests within the window.
- **Fixed — API/web security headers**: added `Content-Security-Policy` (strict
  `script-src`, no `unsafe-inline`) and `Permissions-Policy`. Required removing the last 3
  inline `onsubmit="confirm(...)"` handlers in favor of a `data-confirm` attribute + new
  `wwwroot/js/site.js`.
- **Fixed — session management**: added an 8-hour absolute session timeout on top of the
  existing 30-min sliding expiration (a `sasms:auth_time` claim + a
  `CookieAuthenticationEvents.OnValidatePrincipal` check in `Program.cs`), since sliding
  expiration alone means an actively-used session never truly ends.
- **Deliberately not implemented** (confirmed with the user, not silently skipped): Redis
  cache, JWT/OAuth2 (already a deliberate rejection — see §4), a message queue, load
  balancing. None of these fit a single-instance small-workforce app; forcing them in would
  be exactly the "unnecessary enterprise-level complexity" the FYP doc warns against.

## 10. Python/OpenCV facial verification + liveness detection (2026-09-01)

The last unbuilt piece from the FYP1 doc's priority order is now built. Three architecture
decisions, confirmed with the user before building rather than assumed:

1. **Browser → ASP.NET Core → Python**, not browser-to-Python directly. The employee's
   browser only ever talks to the already-authenticated SASMS app; SASMS calls Python
   server-to-server. `face-service/` binds to `127.0.0.1` only — never exposed to the
   internet or LAN.
2. **OpenCV's built-in LBPH recognizer** (`cv2.face.LBPHFaceRecognizer`), not dlib/
   `face_recognition`. Matches the doc's stated "OpenCV" stack, installs on Windows with a
   plain `pip install` (no compiler toolchain) — confirmed clean during setup.
3. **Liveness = blink detection** over a ~2 second frame burst, using OpenCV's bundled Haar
   cascades. No extra model needed; simple enough to fully explain in a viva.

**New component** `face-service/` (Python 3.12, venv at `.venv/`): Flask app exposing
`POST /register` and `POST /verify`, both behind a shared-secret header
(`X-Face-Service-Secret`, constant-time compare — same pattern as the ASP.NET Core side's
own verification callback). `face_engine.py` has the actual CV logic; its two tuning knobs
if match/liveness need adjusting after real testing are `MATCH_CONFIDENCE_THRESHOLD` and
`MATCH_VOTE_FRACTION`. `GET /test` serves a dev-only webcam test harness for exercising the
service in isolation.

**ASP.NET Core side**: `IFaceVerificationClient`/`FaceVerificationClient` (typed HttpClient,
graceful transport-failure handling). `AttendanceController` gained `Capture` (GET, webcam
UI — redirects to face registration first if not yet registered), `RegisterFace`, and
`CheckIn`/`CheckOut` now accept a JSON frame-burst body and call the face client
synchronously in-process (see §3's note on the two integration paths). The JSON POSTs
needed antiforgery support for `fetch()` since there's no `<form>`: a `X-CSRF-TOKEN` header
read from a `<meta>` tag in `_Layout.cshtml`, configured via
`AddAntiforgery(options => options.HeaderName = ...)`.

**Bug found and fixed the same session**: check-in/out/registration were originally
`[Authorize(Roles = RoleNames.Employee)]` only, silently locking Manager/PIC out entirely —
contradicts README §6.2's "Perform their own attendance check-in/check-out where
applicable." Fixed: open to any authenticated user; a new shared
`Views/Shared/_TodayAttendanceCard.cshtml` partial is used by both dashboards instead of
duplicating the card markup.

**Status: built, tested end-to-end with a real USB webcam, and hardened after real-world
findings (see §11).** Registration, check-in, and check-out all work against a live face;
negative cases (face mismatch, static-photo liveness failure) correctly fail.

## 11. Real-webcam testing round: bugs found and fixed, plus new features (2026-09-01 to 2026-09-04)

Once the user tested with a real USB webcam, several real bugs surfaced (found via direct
MySQL queries + audit-log inspection, not guessing) and were fixed the same session:

- **Checkout bypassing a failed check-in** — `CheckOut` never checked
  `VerificationStatus` before proceeding; fixed by gating check-out on the day's check-in
  actually being `Verified`.
- **"Manually Verify" missing for `Failed` records** — condition only matched `Pending`;
  widened to `!= Verified`.
- **TempData status messages not appearing** — `face-capture.js`'s `fetch()` used default
  `redirect: 'follow'`, which silently performed a phantom second request that consumed the
  TempData message before the real page load. Fixed with `redirect: 'manual'` plus an
  explicit `data-redirect-url`.
- **No manual override for a failed check-out scan** — added
  `ManualCheckOutOverrideAsync`/`ManualCheckOut` action + button for when check-in is
  already Verified but the check-out scan keeps failing.
- **Face-match threshold too strict** — real matches were failing; loosened
  `MATCH_CONFIDENCE_THRESHOLD` 70.0 → 100.0 in `face_engine.py` after user testing.

New features added in the same window:
- **Permanent employee delete** (`EmployeeService.DeleteAsync`) alongside the existing
  soft-delete Deactivate — only allowed when the employee has no attendance/schedule
  history, wrapped in an execution-strategy transaction, best-effort calls
  `FaceVerificationClient.DeleteFaceAsync` (face-service can't "forget" one person from an
  LBPH model, so it retrains from the remaining employees' stored photos — see
  `face_engine.delete_employee`/`_rebuild_model`). **Later removed entirely** per supervisor
  feedback — see §13, Phase 1D. `FaceVerificationClient.DeleteFaceAsync` itself (the HTTP
  client method) was left in place, now unreferenced from the C# side, in case a future
  admin action needs it again — the Python `DELETE /faces/<employee_id>` endpoint it calls
  still exists in `face-service/app.py`.
- **Manager-only password reset** for any employee's login, plus a show/hide toggle on
  every password field (`Views/Shared/_PasswordToggleButton.cshtml` + `site.js`).
- **Attendance is now recorded only on verified success** (README §9's own requirement) —
  previously an Attendance row was created immediately as `Pending` and updated afterward;
  now `AttendanceController.CheckIn` calls face verification *first* and only ever creates
  a row via `AttendanceService.RecordVerifiedCheckInAsync` on real success. A failed attempt
  leaves no Attendance row at all — only an audit log entry.
- **3-attempts-per-day cap on live verification**, tracked separately for check-in and
  check-out (`Employee.FailedCheckInAttempts`/`FailedCheckOutAttempts`/`AttemptsResetDate`,
  migration `AddFailedVerificationAttemptsToEmployee`, `AttendanceService.MaxAttemptsPerDay
  = 3`). After 3 failed attempts on either action, that action is blocked until a
  Manager/PIC intervenes via the new `Employees/Details` "Reset Attempts" button
  (`AttendanceController.ResetAttempts`) or, if no record exists yet for the day, "Manually
  Check In" (`AttendanceController.ManualCheckIn` → `ManualCheckInOverrideAsync`). Counters
  auto-roll over on a new calendar day.

## 12. Broader Testing/UAT status

Per the FYP1 doc's own phases: negative-case regression pass (e.g. confirming the
attempt-block/reset flow end-to-end with a real webcam) is still worth doing, but was
superseded in priority by the supervisor feedback round below (§13) once that arrived.

## 13. Supervisor feedback implementation (started 2026-09-24)

The user's FYP supervisor reviewed the system and requested 7 changes. Full analysis (what
already existed vs. what needed building, DB/file/security impact, PWA feasibility, and a
6-phase implementation order) was produced first and confirmed with the user before any
code was touched — see the memory file `sasms-supervisor-feedback` for the complete
requirement-by-requirement writeup; this section tracks only implementation status.

**Key finding from the analysis**: requirement #1 (Manual Absent by Manager/PIC) was
**already fully built** before the supervisor even asked — `AttendanceService.MarkAbsentAsync`
+ `AttendanceController.MarkAbsent` + the "Mark an employee absent" form on
`Views/Attendance/All.cshtml`, already audit-logged, already refuses to overwrite an
existing record for that date. Phase 2 is therefore a regression check, not new
development.

**Phase 1 — complete, built clean (0 errors/warnings)** (2026-09-24 to 2026-09-25):
- **Phone number compulsory**: `[Required]` + a Malaysian-format `[RegularExpression]`
  (`^0\d{1,2}-?\d{6,8}$`, covers mobile `01X-XXXXXXX(X)` and landline `0X-XXXXXXXX`) added
  to `EmployeeCreateViewModel`/`EmployeeEditViewModel`. **Deliberately NOT enforced at the
  DB level** — a live check found 8 of 9 existing employees have a NULL phone; a NOT NULL
  migration would fail against that data, and the user explicitly said not to invent
  placeholder numbers. Opening any of those 8 in Edit now forces a valid phone before
  saving, which is the intended natural cleanup path — once all 8 are fixed, the DB
  constraint can be added as a follow-up migration.
- **Uppercase normalization**: `EmployeeService.NormalizeToUpper` (trims + uppercases)
  applied to `FullName`, `Position`, `Department` in both `CreateAsync`/`UpdateAsync` —
  never applied to Email/Phone/Username/passwords. User also asked for **live visual
  uppercase while typing** on Create/Edit forms — done with Bootstrap's built-in
  `text-uppercase` utility class on just those three inputs (no new CSS/JS needed; purely
  cosmetic, doesn't touch the submitted value, server-side normalization remains the actual
  source of truth even if JS were bypassed).
- **Deactivated-login message reworded**: only the string in `AccountController.cs`'s
  `user.Status != UserStatus.Active` branch changed, to "This account has been deactivated.
  Please contact your Manager/PIC for assistance." Underlying status logic untouched.
- **Delete Permanently removed**: `EmployeesController.Delete` action, `IEmployeeService.
  DeleteAsync`/`EmployeeService.DeleteAsync`, and the button/form on `Views/Employees/
  Details.cshtml` all removed outright (not just hidden). Deactivate/Reactivate untouched.
  See the §11 note above re: `FaceVerificationClient.DeleteFaceAsync`.

**Phase 2 — complete, verified by the user** (2026-09-25): confirmed all 11 of the
supervisor's Manual Absent requirements were already satisfied by existing code (Manager-only
authorization, uses the existing `Attendances` table, prevents duplicates, refuses to
overwrite an existing record, unchanged Clock In/Out, appears correctly in the Absence and
Monthly reports, audit-logged). No code changes were needed for the checklist itself — but
manual testing surfaced a real display bug, fixed the same session:

- **`VerificationStatus.NotApplicable` added** (`Models/Enums/Enums.cs`) — `MarkAbsentAsync`
  was setting `VerificationStatus = Verified` on Absent records, which falsely implied a
  passed facial scan. No facial verification is attempted for a Manual Absent, so it now
  gets `NotApplicable` (displays as "N/A"). No migration needed — `VerificationStatus` is a
  plain string column with no CHECK constraint. Also fixed: the "Manually Verify (test only)"
  button on `Views/Attendance/All.cshtml` no longer appears on Absent rows (it previously
  showed for any non-Verified record, absences included, which made no sense — there's
  nothing to verify on an absence). One pre-fix test row (TEST EMPLOYEE, 2026-09-25) was
  corrected via a direct one-row `UPDATE` after the user flagged it.
- Confirmed separately: **no Audit Log UI exists** (backend-only, `AuditLogs` table +
  `AuditService.LogAsync`, no controller/view) — `MarkAbsent` is logged there, verified live
  against the DB. Building a UI for it was explicitly declined by the user for now.

**Phase 2.5 — SCHEDULE-GATED ATTENDANCE — complete, verified by the user** (2026-09-25,
inserted before Phase 3 after the user identified a gap during Phase 3 planning): the
`Schedules` table is now the sole authority for "is this employee expected to work today" —
no Full-Time/Part-Time field exists or was added; a "rest day" is simply a date with no
`Schedule` row, and produces no `Attendance` record at all (not Absent, not On Leave).
- **`IAttendanceService.HasScheduledShiftAsync(employeeId, date)`** — new shared check
  (`_db.Schedules.AnyAsync(...)`), used everywhere schedule eligibility now matters.
- **`AttendanceController.CheckIn`** — checks this *before* calling the face-verification
  client at all; an unscheduled employee never triggers a camera call, never gets an
  attendance row, never burns an attempt.
- **`AttendanceService.RecordVerifiedCheckInAsync`** — defense-in-depth: previously defaulted
  a missing schedule to `AttendanceStatus.Present`; now refuses outright
  (`ServiceResult.Fail("You are not scheduled to work today.")`) if no schedule exists, even
  if this method were somehow reached without going through the controller gate.
- **`AttendanceService.MarkAbsentAsync`** — now refuses to mark an employee absent on a date
  with no `Schedule` row ("This employee is not scheduled to work on the selected date and
  cannot be marked absent.") — a genuine correction to Phase 2 behavior, not new Phase 3 work.
- **`AttendanceService.ManualCheckInOverrideAsync`** — same gate applied here too (a follow-up
  fix after the user caught this override was the one path still missing it) — a
  Manager/PIC override bypasses the *camera*, not the schedule requirement.
- **Employee dashboard** (`AttendanceStatusCardViewModel.HasScheduleToday`,
  `HomeController.BuildAttendanceStatusCardAsync`, `_TodayAttendanceCard.cshtml`) — shows
  "You are not scheduled to work today." with no Check In button at all on a rest day,
  instead of an enabled button that would just get rejected server-side.
- No schema change — this is a set of new/changed queries and checks against the
  already-existing `Schedules` table.

**Phase 3 (Leave Application) — COMPLETE, built clean, awaiting the user's manual test pass**
(2026-09-29 to 2026-09-30). Approved business rules (all explicitly confirmed with the user
before implementation, see [[sasms-supervisor-feedback]] for the full back-and-forth):
Employee-only submission (no Manager/PIC self-leave), Manager/PIC-only approve/reject,
all-or-nothing multi-day approval (any conflicting date blocks the entire approval, nothing
partial), no `LeaveType` field (minimum scope only — Start/End/Reason/Status/review metadata),
cross-employee overlap is explicitly allowed (multiple employees can request the same date;
the overlap check only applies to one employee's own Pending/Approved applications), and
approved leave creates real `Attendance` rows (`AttendanceStatus.OnLeave`,
`VerificationStatus.NotApplicable`) **only for dates inside the range that also have a
Schedule row** — a rest day inside an approved range gets no row at all.

- **New**: `LeaveApplication` entity + `LeaveStatus` enum (Pending/Approved/Rejected) +
  migration `AddLeaveApplications` (one new table, FKs to `Employees`/`Restrict` and
  `Users`/`SetNull`, no schema change to any existing table) + `ILeaveService`/`LeaveService`
  + `LeaveController` + `Views/Leave/{Apply,Index,Review}.cshtml` + a "Leave" nav link.
- **Validation**: `EndDate >= StartDate`; `StartDate >= IBusinessClock.Today` (no
  past-dated leave); overlap check scoped strictly to the submitting employee's own
  Pending/Approved applications (Rejected ones never block).
- **Approval algorithm**: find scheduled dates in range → if ANY already has an `Attendance`
  row of any status, block the whole approval and name the conflicting date(s) → otherwise
  insert one `OnLeave`/`NotApplicable` row per scheduled date and mark the application
  Approved, all in one `SaveChangesAsync()` call (atomic by default; no manual transaction
  needed since there's only one save point, unlike `EmployeeService.CreateAsync`'s two-step
  case) with a `DbConcurrencyHelper`-caught race guard. `Schedule` rows are never touched.
- **Reused, not duplicated**: `INotificationService` (Leave Approved/Rejected to the
  employee), `IAuditService` (`SubmitLeaveApplication`/`ApproveLeaveApplication`/
  `RejectLeaveApplication`), `VerificationStatus.NotApplicable` (already existed from Phase
  2), `IBusinessClock` (all date validation — see §14 below), `ReportService` (genuinely
  **zero** changes — `GetMonthlyAsync`'s `DaysOnLeave` count already worked the moment real
  `OnLeave` rows existed).
- **Security simplification worth knowing**: there is no id-based "view a single
  application" route for Employees at all — `LeaveController.Index` only ever returns
  applications pre-scoped to `EmployeeId == currentUser.EmployeeId` from the service query
  itself, so there's no guessable URL to defend against (IDOR concern resolved by
  construction, not by a runtime ownership check).
- **Narrowly-scoped Phase 2 touch-up, explicitly approved**: `AttendanceController.CheckIn`'s
  existing-record message is now status-aware — an employee hitting Check In on an
  approved-leave day sees "You are on approved leave today and cannot check in." instead of
  the misleading "You have already checked in today." Nothing else in `CheckIn`/`CheckOut`
  changed.
- **Two post-implementation bugfixes found during the user's manual testing, same session**:
  (1) the "Manually Verify (test only)" button/action didn't account for OnLeave records
  (only excluded Absent) — fixed both in the UI (`Views/Attendance/All.cshtml`, now keyed off
  `VerificationStatus == Pending || Failed` rather than excluding specific statuses one at a
  time) and, more importantly, **server-side** in `AttendanceService.ManualVerificationOverrideAsync`/
  `ManualCheckOutOverrideAsync` (both now refuse outright on `VerificationStatus.NotApplicable`
  records — a direct POST bypassing the UI could previously still corrupt an Absent/OnLeave
  record; confirmed fixed via a throwaway in-memory-EF-Core test harness built and deleted in
  the same session, 14/14 assertions passed, zero real dev data touched). (2) The employee
  dashboard's Check-Out button didn't account for `FaceRegistered` the way the Check-In
  button already did — fixed to disable with "Register your face first" when verified-but-
  unregistered, matching the Check-In pattern; confirmed the underlying server-side gate
  (`Capture` GET redirecting to registration) already protected against actual misuse, this
  was UI-only.

## 14. Malaysia business-date & display-timezone fix (2026-09-29, discovered mid-Phase-3-planning)

While preparing Phase 3, the user caught a real bug during early-morning testing: `today`
throughout `AttendanceService`/several controllers was computed as
`DateOnly.FromDateTime(DateTime.UtcNow)` — correct for a UTC *instant*, wrong for "what
Malaysia calendar day is it," since Malaysia is UTC+8 with no DST. Between roughly
00:00–08:00 local time each day, the UTC date is still *yesterday*, which caused real,
data-affecting bugs: `RecordVerifiedCheckInAsync` could **write** the wrong `AttendanceDate`
onto a genuine check-in during that window (one historical row, `AttendanceId 1`, was found
provably affected and reported to the user, not silently corrected), the schedule-gate could
reject a real scheduled employee, and `AttemptsResetDate` comparisons could misfire.

**Fix**: `Security/IBusinessClock`/`BusinessClock` — a small centralized service (singleton,
DI-registered) that resolves the Malaysia `TimeZoneInfo` once (tries IANA `Asia/Kuala_Lumpur`
first for Linux/portable correctness, falls back to Windows' `Singapore Standard Time`, and
as a last resort a `TimeZoneInfo.CreateCustomTimeZone` fixed +8:00 zone — genuinely correct
for Malaysia specifically, since it never observes DST). Exposes `DateOnly Today` (replaces
every business-date `UtcNow`/`DateTime.Today` call across `AttendanceService`,
`AttendanceController`, `HomeController`, `EmployeesController`, `ReportsController`,
`SchedulesController`, and the Mark Absent date-picker default) and
`DateTime ConvertBusinessLocalToUtc(DateOnly, TimeOnly)` (fixed a second, more severe bug:
the Late/Present calculation was comparing a Schedule's Malaysia-local `StartTime` against
real UTC-now as if `DateTimeKind.Utc` performed a conversion — it doesn't, .NET's `DateTime`
comparisons ignore `.Kind` entirely, so this silently added an ~8-hour leniency to the Late
determination on *every* check-in, not just near midnight).

**Separate follow-up, same investigation**: stored timestamps (`CheckInTime`/`CheckOutTime`/
`CreatedAtUtc`) are correctly kept as raw UTC in the database — that was never the problem —
but several *display* locations rendered them raw instead of converting for the viewer
(`Attendance/Index.cshtml`, `Attendance/All.cshtml`, `_TodayAttendanceCard.cshtml`,
`Reports/Late.cshtml`, and `Notifications/Index.cshtml`'s `.ToLocalTime()`, which — like the
original bug — silently depended on the server's OS-configured timezone rather than a
deliberate Malaysia conversion). Fixed with one more `IBusinessClock.ToMalaysiaTime(DateTime)`
method (reusing the same cached `TimeZoneInfo`), injected into each of those views. The Late
report's CSV export also now converts, via a small dedicated `LeaveAttendanceExportRow`-style
projection (`ReportDtos.LateAttendanceExportRow`) built in `ReportsController.LateExport`,
with a `[Display(Name = "Check In (Malaysia Time)")]`-driven header —
**`CsvExportHelper`'s core formatting logic itself was not touched**, so every other export
is unaffected; only the optional `[Display]` header-override lookup was added, which is inert
unless a DTO opts in.

**No schema/migration changes for any of this** — entirely application-layer logic and one
new interface+implementation.

**Rule for all future work on this project**: any code representing "what Malaysia business
day/time is it" must go through `IBusinessClock`, never `DateTime.UtcNow`/`DateTime.Today`/
`DateTime.Now`/`.ToLocalTime()` directly. Genuine timestamps (audit logs, `CreatedAtUtc`,
`CheckInTime`/`CheckOutTime` as *stored* values) correctly remain raw UTC — only *display* of
those values needs `ToMalaysiaTime`.

**Phases 4 and 5 — complete, manually verified on a physical iPhone (see §16, §17). Phase 6
(final regression pass / FYP2 write-up) not yet started.**

## 16. Phase 4 — Employee Mobile/iOS PWA (2026-09-30)

Built and manually verified on a real physical iPhone (Safari), not just responsive-mode
emulation — several bugs below were only caught this way.

- **4.1 PWA installability foundation**: web app manifest, icon set, and the required
  `<meta>`/`<link>` tags for "Add to Home Screen" on iOS Safari.
- **4.2 Employee-facing table responsiveness**: attendance/schedule/leave tables wrapped for
  horizontal scroll on narrow viewports (`.table-responsive` pattern).
- **4.3 Employee mobile navigation and layout polish**: navbar and footer reworked for phone
  width.
- **Real-device bug — iOS Safari touch-scroll**: `overflow-x: hidden` on `<body>` silently
  broke the native touch-scroll *gesture* on nested `.table-responsive` regions in real
  Safari, even though standard CSS box-model reasoning says ancestor `overflow` shouldn't
  affect a descendant with its own `overflow-x: auto`. Confirmed only by physical-device
  testing (desktop emulation didn't reproduce it). Fixed by removing `overflow-x: hidden`
  from `body`, keeping it only on `html`.
- **Real-device bug — sticky footer**: two earlier approaches (`position: absolute` fixed
  60px, then a flex-column layout relying on `body { min-height: 100% }`) both failed on
  the real device — the second because `html` only ever had `min-height` (never a resolved
  `height`), so `body`'s percentage `min-height` had nothing to resolve against. Fixed with
  `min-height: 100vh` (then `100dvh` as a progressive enhancement) on a flex-column layout.

## 17. Phase 5 — HTTPS + physical-iPhone facial verification testing (2026-09-30 to 2026-10-01)

### 5.1 — Local HTTPS for physical-device camera access

`getUserMedia()` requires a secure context; a LAN IP over plain HTTP doesn't qualify (only
`localhost` does), so physical-iPhone camera testing needed real, trusted HTTPS across
devices. Built with **mkcert** (local CA + LAN-IP-SAN certificate) rather than
`dotnet dev-certs https` (that cert's SAN is `localhost`-only and trust is single-machine).

- `Program.cs`: if `src/SASMS.Web/dev-https-cert/` exists (gitignored, machine-local), an
  explicit `ConfigureKestrel` block listens on both 5080 (HTTP) and 5443 (HTTPS) — ASP.NET
  Core's documented precedence means explicit `Listen*` calls *replace* `--urls`/
  launchSettings, so both had to be configured together, not just HTTPS added on top.
- Hit and fixed a Windows SChannel limitation: `X509Certificate2.CreateFromPemFile()`
  produces a cert with an "ephemeral" key that Windows' TLS stack can't reliably use
  server-side — fixed via an in-memory PFX re-import round-trip.
- The mkcert root CA (`rootCA.pem`, no private key) was installed as a trusted profile on
  the physical iPhone; the CA's own private key never leaves `%LOCALAPPDATA%\mkcert` and is
  not inside the project.
- **Manually verified on the physical iPhone**: SASMS loads over trusted HTTPS (no
  certificate warning) and Safari grants camera access.

### 5.2 — Facial verification + liveness, real-device testing and fixes

Real-iPhone testing surfaced two genuine problems, each diagnosed from actual evidence
(console diagnostics, direct DB/file inspection) before any fix was made — not guessed at.

**Problem 1 — Haar-eye-cascade liveness was unreliable on the iPhone camera.** Diagnostic
logging added to `face_engine.py` (console-only; never in the HTTP response, never persisted,
no image data) showed the Haar eye cascade reporting "2 eyes detected" on **every single
frame** of two separate bursts where the person visibly blinked — it never registered a
closed eye at all on that camera/lighting. Root cause: Haar's eye cascade keys on
eye-region contrast/structure (eyebrow, socket, eyelid crease) more than on whether the
eyelid is actually shut — a known weakness, not a threshold problem (LBPH face-match
confidence was consistently ~30, well within range; only liveness was affected).

**Fix**: replaced Haar-eye-cascade liveness with **eye-aspect-ratio (EAR) computed from
MediaPipe's local `FaceLandmarker` model** (`face-service/models/face_landmarker.task`, a
one-time-downloaded ~3.7MB file from Google's official MediaPipe model storage — gitignored,
see `face-service/README.md` for the download step; all inference is local, no cloud/paid
API calls at request time). The liveness *rule* itself is unchanged — still requires a
temporal open → closed → open transition across the frame burst (`_detected_blink`,
untouched) — only the per-frame open/closed signal changed, from "≥2 Haar eye detections" to
"EAR ≥ 0.21" (`EAR_CLOSED_THRESHOLD`, the literature-standard starting point). New dependency:
`mediapipe==1.0.1` (forced a `numpy` bump to 2.5.3 as its transitive requirement; verified
compatible with the existing `opencv-contrib-python`/LBPH code via a direct smoke test before
relying on it).

**Manually verified on the physical iPhone**:
- Real face + real blink → `livenessPassed=True`, attendance recorded as Verified (both
  check-in and check-out).
- A static photo of the correct registered person's face held up to the camera → eyes
  stayed reported as OPEN throughout the burst → `livenessPassed=False` → check-out
  correctly rejected.
- **Verified scope, stated precisely**: this defeats a held-up static photo. It is
  **not** validated against (and should not be described as protecting against) replayed
  video, 3D masks, deepfakes, or other advanced presentation attacks — those are out of
  scope for this FYP.

**Problem 2 — duplicate biometric test-data caused face-match to appear inconsistent.**
Deeper diagnostic logging (now also reporting each frame's `predictedEmployeeId`, not just
match/no-match) traced a second, unrelated issue: during earlier testing sessions, the same
physical test person had been registered as the "employee" under **six different employee
IDs** (2, 3, 4, 6, 9, 10 — confirmed by directly viewing the stored reference photos for
each), plus a stale label/model entry for a seventh (employee 5) whose photo directory had
been removed outside the normal delete flow at some point. OpenCV's LBPH `predict()` searches
for the nearest match across *every* label in the shared model regardless of which identity
is claimed — with near-duplicate faces trained under multiple labels, which label "wins" for
a given frame becomes sensitive to minor per-frame noise, so the same physical person could
match as employee 2 on one burst and as a different employeeId on the next, with confidence
staying low (a close match was still found) but under the wrong label. **Not a threshold or
algorithm bug** — the model behaved correctly given duplicate training data.

**Fix**: cleaned via the existing Manager/PIC **Reset Face Registration** feature (see
below) run once per duplicate employee ID, going through the proper
delete-from-face-service → rebuild-model path rather than manual file deletion. Employee 2
("Test Employee") kept as the sole canonical enrollment; employees 3, 4, 5, 6, 7, 8, 9, 10
had their face registration reset.

**New feature, built this session**: Manager/PIC-only **Reset Face Registration**
(`EmployeesController.ResetFaceRegistration`, `EmployeeService.ResetFaceRegistrationAsync`,
button on `Employees/Details.cshtml`, shown whenever `Employee.FaceTemplateRef` is set).
Deletes the employee's reference photos from the face-service and rebuilds `model.yml`
without them (confirmed via code trace this also correctly handles an employee whose photo
directory is already missing, e.g. the employee-5 case above), then clears
`Employee.FaceTemplateRef` **only after** the face-service confirms deletion succeeded — the
database never claims "unregistered" while stale biometric data might still exist.
Confirmed by code trace that this touches only `Employee.FaceTemplateRef`: no effect on
attendance history, schedules, leave applications, accounts/passwords, or employment status.
This is a reset/re-enrollment action, **not** employee deletion (`Delete Permanently` stays
removed per §13 Phase 1D). Separately, the existing "Reset Attempts" button's visibility
condition was loosened from "fully blocked" to "any failed attempts used today > 0", so a
Manager/PIC can clear a partial attempt count before it blocks the employee, not only after.

**Final manually-verified real-iPhone flow, end to end**: HTTPS → Safari camera → face
registration → face re-registration after Reset → real face + blink Check In → Verified →
static-photo Check Out attempt → rejected by liveness → real face + blink Check Out →
Verified.

**Cleanup after testing completed**: the dev-only verbose diagnostic logging added to
`face_engine.py` during this investigation (per-frame confidence, predicted label,
EAR values, O/C history — console-only, never returned to the browser/ASP.NET Core, no
image data ever logged or stored) is left in the code but **disabled by default**
(`DIAGNOSTIC_LOGGING = False`) now that both issues are resolved, rather than deleted, in
case a similar issue needs diagnosing again later.

## 18. What's next

Phases 1–5 are complete and manually verified (Phase 3 on desktop/webcam; Phases 4–5 on a
real physical iPhone). Next per the supervisor's 6-phase order: **Phase 6 — final
regression pass**, plus the still-deferred negative-case regression pass from §12. After
that: FYP2 documentation/write-up and deployment/demo to the client.

**Phase 6 status (2026-10-01): test plan produced, execution not started.** A full
regression checklist was written covering all 11 functional areas (auth, employee mgmt,
scheduling, attendance, leave, facial verification, reports, notifications, audit log,
mobile/PWA/HTTPS, security/negative tests) — 71 MUST-TEST-NOW items, 7 accepted as
already verified from Phase 5 evidence (no need to repeat), 24 optional. It reuses three
existing test identities (Manager `admin`, Employee 2 for facial tests, Employee 3 for
non-biometric tests, Employee 10 for the one deactivate/reactivate test) rather than
creating new employees, and sequences batches so later ones (Reports, Audit) consume data
earlier ones create. **The full checklist itself was not saved as a file in this repo** —
it exists in that session's conversation; regenerate it from the current codebase (same
method: read the controllers/services directly, don't assume) if it's needed again and
isn't available, rather than reusing a stale copy.

## 19. Dashboard/UX enhancements and production-wording cleanup (2026-10-01, after Phase 6 planning)

Several small, independently-authorized changes landed after the Phase 6 test plan was
written but before execution started — each built clean (0 errors/warnings):

- **Leave Application now notifies Manager/PIC on submission.** `LeaveService.SubmitAsync`
  (after the save succeeds, so a validation/DB failure never notifies) looks up every
  *active* `Manager/PIC`-role user (`_db.Users.Where(u => u.Role.RoleName ==
  RoleNames.Manager && u.Status == UserStatus.Active)` — there's no single designated
  approver in this schema, so all of them are notified) and calls the existing
  `INotificationService.NotifyAsync` once per manager. Reused infrastructure only; no new
  notification table/service.
- **New "Pending Leave" card on the Manager Dashboard** (`ILeaveService.GetPendingCountAsync`
  — a single `COUNT` query, `LeaveStatus.Pending` only) plus a "Review Leave Applications"
  link added to the dashboard's existing action-button row.
- **Dashboard stat-card row layout fixed**: the 5 "Store Overview" cards (4 original +
  Pending Leave) now use Bootstrap's `row-cols-2 row-cols-sm-3 row-cols-lg-5` utility
  instead of fixed `col-md-3`, so all 5 sit in one row on desktop and wrap cleanly on
  smaller screens — no custom CSS added, pure Bootstrap-utility markup change.
- **"(test only)" wording removed from every Manager/PIC-facing UI string** ahead of
  production: the 3 manual-override button labels (Manually Verify / Check Out / Check In),
  their 3 confirm-dialog messages, 1 muted helper line, and 3 post-action `TempData`
  status messages. **Deliberately left unchanged** (per explicit instruction, both still say
  "test-only"): the `AuditLogs.Description` strings written by
  `AttendanceService`'s three manual-override methods, and the XML doc comments on
  `IAttendanceService` — those aren't user-facing and changing the audit text specifically
  was ruled out.
- **New "Work Hours Overview" section on the Manager Dashboard**, inspired by the user's
  own reference to tools like Jibble: a Today/This Week/This Month period selector, 3
  summary stats (Total Worked Hours, Overtime Hours, Employees Tracked), and one pure-CSS
  horizontal stacked bar chart (green = regular hours, orange/`--status-warning` = overtime
  — no charting library). **Built entirely on `IReportService.GetWorkHoursAsync`**, a new
  method factored out of the Reports page's existing worked-hours formula — `GetOvertimeAsync`
  was refactored to call it too, so there is now exactly one copy of "hours worked from a
  Verified attendance record" in the codebase, eliminating what had been silent duplication
  between `GetMonthlyAsync` and `GetOvertimeAsync`. The dashboard and the Overtime/Monthly
  reports can't disagree because they share this one source.
- **Period-switch UX went through three iterations** before landing correctly — worth
  knowing if asked to touch this again: (1) full-page GET reload with `#anchor`/
  `asp-fragment` — technically scrolled to the right place but visibly jumped from top first;
  (2) `sessionStorage` + `window.scrollTo` on load — still raced against the browser's own
  default scroll-restoration and the page's not-yet-final layout height; (3) **adding
  `history.scrollRestoration = 'manual'` plus deferring the restore to `window.onload` +
  `requestAnimationFrame`** — still visibly jumped, because any full-page navigation
  inherently starts at the top for one paint before JS can act. The user then explicitly
  ruled out *all* further scroll-timing hacks and asked for the real fix: **the period
  selector no longer navigates the page at all.** `Views/Home/_WorkHoursOverview.cshtml`
  (new partial) + `HomeController.WorkHoursOverviewPartial` (new lightweight GET action,
  `[Authorize(Roles = RoleNames.Manager)]`, returns `PartialView(...)` with no layout) let a
  small `fetch()` in `ManagerDashboard.cshtml` swap just `#work-hours-overview-container`'s
  `innerHTML` in place. The period links keep a working plain `href` as a no-JS fallback.
  **Lesson for future "don't move the user's scroll position" requests in this app: prefer
  an in-place AJAX partial swap over any scroll-save/restore technique — the latter is
  fighting the browser, not cooperating with it.**

## 20. Phase 6 execution — started 2026-10-02, two real bugs found in facial verification

Real physical-iPhone testing began on the facial-verification batch: real registered face
→ Verified (correct); wrong face → rejected (correct); **static-photo spoof → unexpectedly
passed and was recorded as Verified/Checked Out**. Spoof method, confirmed by the user: a
friend's selfie displayed on his *phone screen*, held close to the iPhone camera, no part
of his real face visible. Full code trace (Python `/verify` route, `face_engine.py`,
`FaceVerificationClient.VerifyAsync`, `AttendanceController`'s decision logic) confirmed the
bug is **Python-side liveness detection only** — response parsing and the ASP.NET
`faceMatchSuccess && livenessPassed` gate are both correct and unweakened.

**Leading hypothesis (unconfirmed)**: `_detected_blink()` accepts a single isolated
"closed" frame as a full blink, with no minimum-duration requirement — and a phone-screen
spoof introduces a specific noise source (screen refresh-rate flicker vs. the iPhone
camera's rolling shutter) that a printed photo wouldn't, which combined with the registered
employee's open-eye EAR baseline (~0.30–0.34, not far above the 0.21 threshold) could
plausibly produce a false single-frame dip. **Not yet confirmed** — a second hypothesis
(two independent face-region checks locking onto two different faces in frame) was ruled
out by the user's own account of the test setup (no real face was in frame at all).

**Update: liveness fix implemented (2026-10-02), not yet confirmed on real hardware.** The
controlled static-photo re-test described above was never actually run — investigation
pivoted to a second bug (below) first. Based on physiological/structural reasoning rather
than a per-attempt diagnostic capture, `_detected_blink()` in `face_engine.py` now requires
**at least 2 consecutive "closed" frames** (was: one isolated frame) before counting a
closure as a genuine blink — verified via 7 direct test-case assertions plus a syntax/import
check, with `EAR_CLOSED_THRESHOLD`/frame timing/face matching all confirmed unchanged. Still
needs a real-device repeat of both the static-photo spoof (should now fail) and a genuine
blink check-in (must still pass) before this can be called resolved.

### Second Phase 6 bug found the same day: face-match false-accept (root-caused, not fixed)

Separate from the liveness bug above. Logged in as Employee 6 (Luqmanul Hakim, genuinely
registered the previous day), checked in presenting **a different real person's face** —
the system returned Verified. Real diagnostic data: 14/15 frames matched Luqmanul's label,
confidence 59–68, `voteFraction=0.93`.

A full layer-by-layer trace (ASP.NET request → Python `/verify` → LBPH `predict()` →
response parsing → `AttendanceController`'s decision gate) ruled out every explanation
except the actual LBPH distance calculation: no label collision (`labels.txt` showed 3
genuinely distinct labels), no ignored/mishandled `employeeId`, no "accept any prediction"
bug, no stale training data, and — confirmed by directly viewing the registered photos —
this is **not** a repeat of the Phase 5.2 duplicate-same-person-multiple-labels bug; all
three registered employees are visibly different real people this time. **Root cause:
`MATCH_CONFIDENCE_THRESHOLD = 100.0` is too loose to reliably discriminate between genuinely
different people** under this system's capture pipeline — a calibration problem, not a logic
bug.

Diagnostic-only cross-label margin logging was added to `verify_face()` (via OpenCV's
existing `StandardCollector`/`predict_collect`, already available in the installed
`opencv-contrib-python` — no new dependency), gated behind `DIAGNOSTIC_LOGGING`, with zero
effect on the actual matching decision. A real bug in this new diagnostic code was caught
and fixed before being reported done: `collector.getResults()` returns one entry per
*training sample*, not per label, so the first version picked an arbitrary sample instead of
the per-label minimum — fixed and re-verified against `recognizer.predict()`'s own value.

**No fix chosen for this bug.** The evidence needed to safely calibrate a margin or tighter
threshold — genuine Luqmanul confidence data, and the false-accept's cross-label margin —
doesn't exist yet (both diagnostic additions came after the relevant attempts happened).
LBPH confidence doesn't reliably transfer between different people, so guessing a number now
risks either missing the real fix or breaking genuine matches. **Next real check-in/out
(any employee) with `DIAGNOSTIC_LOGGING` on supplies the missing data — only then should a
`MATCH_MARGIN` or threshold change be chosen.**

**Current repo state**: `DIAGNOSTIC_LOGGING = True` still. See [[sasms-facial-verification]]
(memory) for full detail on both bugs — **do not describe facial verification (liveness or
face matching) as fully working in any future summary until both are resolved and
re-tested.**
