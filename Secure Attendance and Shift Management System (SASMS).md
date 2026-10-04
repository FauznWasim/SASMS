# Secure Attendance and Shift Management System (SASMS)

## 1. Project Overview

**Project Name:** Secure Attendance and Shift Management System (SASMS)  
**Project Type:** Final Year Project (FYP) – Cybersecurity / Web Application  
**Client:** Subway Metro Point Complex  
**Primary Users:** Employee, Store Manager, PIC (Administrator)

SASMS is a web-based attendance and employee shift management system proposed for Subway Metro Point Complex.

The system is designed to improve the management of employee attendance and work schedules while addressing attendance fraud such as **buddy punching**.

The main security feature is attendance verification using **facial recognition combined with basic liveness detection**. The system also implements **Role-Based Access Control (RBAC)** to separate employee and management privileges.

> Important: The project has completed the FYP1 analysis and design stage. The actual system implementation will be developed during FYP2.

---

# 2. Problem Statement

The project addresses three main problems identified from the current attendance and workforce management process.

### Problem 1 – Attendance Record Management

Managing and maintaining employee attendance records efficiently is difficult under the current process.

Attendance information needs to be properly recorded, monitored, and retrieved by management.

### Problem 2 – Attendance Fraud and Weak Verification

The existing attendance process has insufficient identity verification.

This creates the possibility of attendance fraud such as **buddy punching**, where another employee may record attendance on behalf of someone else.

### Problem 3 – Employee Shift Management

Employee work schedules and shift arrangements require management effort and can become difficult to monitor and coordinate.

A dedicated scheduling function is required to manage employee work schedules and shift assignments.

---

# 3. Project Objectives

The objectives correspond directly with the three identified problems.

### Objective 1
Develop an efficient employee attendance management system.

### Objective 2
Implement secure attendance verification mechanisms using facial recognition and liveness detection.

### Objective 3
Develop employee shift scheduling and monitoring features.

---

# 4. Proposed Technology Stack

The technology stack established during FYP1 should remain consistent unless changes are formally approved.

| Component | Technology |
|---|---|
| Operating System | Windows 11 |
| Code Editor | Visual Studio Code |
| Backend Framework | ASP.NET Core MVC |
| Facial Verification Programming | Python |
| Computer Vision | OpenCV |
| Frontend | HTML, CSS, JavaScript, Bootstrap |
| Database | MySQL |
| Local Development Environment | XAMPP |
| Browser / Testing | Google Chrome |

---

# 5. Proposed Architecture

The application will primarily use **ASP.NET Core MVC** as the web application framework.

Conceptually:

```text
Employee / Manager
        |
        v
Web Browser
        |
        v
ASP.NET Core MVC Application
        |
        +----------------------+
        |                      |
        v                      v
     MySQL               Python/OpenCV
     Database            Facial Verification
                              +
                       Liveness Detection
```

ASP.NET Core MVC handles the primary web application functionality, while Python and OpenCV are intended for the facial verification and basic liveness detection functionality.

The exact integration mechanism between ASP.NET Core and the Python/OpenCV component is not finalized in FYP1 and should be designed during implementation.

---

# 6. User Roles

The system has two main actors.

## 6.1 Employee

Employees should be able to:

- Log into the system using their registered account.
- Check in using facial verification.
- Check out using facial verification.
- View their attendance history.
- View their assigned work schedule.
- View working hours.
- Receive notifications regarding attendance or schedule information.
- Access only functions permitted to the Employee role.

---

## 6.2 Store Manager / PIC (Administrator)

Managers/PIC should be able to:

- Log into the management interface.
- Perform their own attendance check-in/check-out where applicable.
- Manage employee information.
- Create and manage employee work schedules.
- Assign employee shifts.
- Monitor employee attendance.
- View attendance history.
- Generate attendance reports.
- Manage notifications.
- Manage user access and roles.
- Access management functionality unavailable to normal employees.

---

# 7. Authentication and Role-Based Access Control

The system should implement normal account authentication together with RBAC.

Example:

```text
Login
  |
  v
Validate Username / Password
  |
  v
Retrieve User Role
  |
  +-------------------+
  |                   |
Employee           Manager/PIC
  |                   |
Employee            Admin
Dashboard          Dashboard
```

A user's role should be stored in the database and checked after successful authentication.

Example conceptual structure:

```text
Users
--------------------------------
UserId
Username
PasswordHash
EmployeeId
RoleId
Status
```

```text
Roles
--------------------------------
RoleId
RoleName
```

Example roles:

```text
1 = Employee
2 = Manager/PIC
```

Authorization should be enforced on the **server side**, not only by hiding buttons in the frontend.

For example, an Employee must not be able to access administrative URLs or APIs even if they manually enter the endpoint.

---

# 8. Attendance Workflow

The intended employee attendance process is:

```text
Employee Login
      |
      v
Employee Dashboard
      |
      v
Select Check-In / Check-Out
      |
      v
Camera Capture
      |
      v
Liveness Detection
      |
      v
Facial Verification
      |
   +--+--+
   |     |
Success Failure
   |     |
   v     v
Record   Retry /
Attendance Reject
   |
   v
Database
```

Attendance should only be recorded after successful verification.

Possible attendance information includes:

```text
AttendanceId
EmployeeId
AttendanceDate
CheckInTime
CheckOutTime
AttendanceStatus
VerificationStatus
```

The final database schema may be refined during FYP2.

---

# 9. Facial Recognition

Facial recognition is one of the primary security mechanisms of SASMS.

The planned technologies are:

```text
Python
   +
OpenCV
```

The general process is:

1. Capture the employee's face through a camera/webcam.
2. Detect the face.
3. Compare/verify the detected face against the employee's registered facial information.
4. Perform liveness verification.
5. Accept or reject the attendance attempt.
6. Record attendance only when verification succeeds.

The purpose is to ensure that attendance is associated with the correct employee.

---

# 10. Liveness Detection

Facial recognition alone may be vulnerable to spoofing attempts involving photographs or recorded images.

Therefore, SASMS includes **basic liveness detection**.

Its purpose is to determine whether the person presented to the camera is physically present rather than simply presenting a static representation of the employee.

The final liveness detection technique is to be implemented and evaluated during FYP2 using Python/OpenCV.

The FYP1 requirement is:

```text
Face Verification
        +
Liveness Detection
        |
        v
Secure Attendance Verification
```

Do not remove liveness detection from the implementation because it is one of the project's main security requirements.

---

# 11. Attendance Management

The attendance module should allow the system to:

- Record employee check-in.
- Record employee check-out.
- Store attendance records.
- Display employee attendance history.
- Allow management to monitor attendance.
- Track working hours.
- Generate attendance information for reporting.

---

# 12. Shift Scheduling

The scheduling module should allow management to:

- Create employee work schedules.
- Assign shifts to employees.
- Update existing schedules.
- View employee schedules.
- Monitor employee work arrangements.

Employees should be able to view their assigned schedules but should not have the same schedule-management permissions as Manager/PIC users.

Conceptually:

```text
Manager/PIC
    |
    v
Create / Update Schedule
    |
    v
Assign Employee
    |
    v
Save Schedule
    |
    v
Employee Views Schedule
```

---

# 13. Reporting

The system should provide attendance-related reports for management.

Required reports identified during requirements gathering include:

- Monthly attendance reports.
- Late attendance reports.
- Absence reports.
- Overtime-related information where applicable.
- Employee attendance history.

Reports are intended to help Store Manager/PIC monitor workforce attendance and working patterns.

---

# 14. Notifications

The system should support notifications relating to employee attendance and schedules.

Examples include:

- Schedule updates.
- Shift changes.
- Attendance-related information.
- Pending actions where applicable.

The exact notification mechanism will be determined during FYP2.

---

# 15. Proposed Database Entities

The final schema has not yet been implemented, but the system will likely require entities similar to:

```text
Users
Employees
Roles
Attendance
Schedules
Notifications
```

Possible relationships:

```text
Roles
  |
  +---- Users
          |
          +---- Employees
                  |
                  +---- Attendance
                  |
                  +---- Schedules
                  |
                  +---- Notifications
```

Database design should support RBAC and maintain separation between authentication information and employee operational data where practical.

---

# 16. Security Requirements

Because this is a cybersecurity-focused FYP, security controls should be treated as core requirements rather than optional features.

Important controls include:

### Authentication
Users must authenticate before accessing protected system functionality.

### Password Security
Passwords should never be stored as plaintext.

Use secure password hashing provided by the chosen ASP.NET authentication mechanism.

### Role-Based Access Control
Employee and Manager/PIC privileges must be separated.

### Server-Side Authorization
Authorization checks must occur on the backend.

### Facial Verification
Attendance should require successful employee facial verification.

### Liveness Detection
Attendance verification should include basic protection against face spoofing.

### Input Validation
All user-controlled input must be validated.

### Session Security
Authenticated sessions should be properly managed and terminated during logout.

### Database Security
Use parameterized queries or ORM mechanisms to reduce SQL injection risk.

### Auditability
Important administrative and attendance actions should be traceable where practical.

---

# 17. Existing Systems Investigated

Three existing systems were studied during FYP1.

## Jibble

Relevant features:

- Facial recognition attendance.
- Timesheets.
- Attendance reports.
- Scheduling.

The investigation identified facial recognition as useful for attendance verification.

However, the reviewed system did not explicitly provide the liveness detection mechanism required by SASMS.

SASMS therefore proposes:

```text
Facial Recognition + Liveness Detection
```

---

## Connecteam

Relevant features:

- Workforce scheduling.
- Communication tools.
- Attendance monitoring.
- Employee management.

The scheduling and workforce management concepts informed the proposed employee scheduling functionality.

---

## Buddy Punch

Relevant features:

- Attendance tracking.
- Scheduling.
- Payroll-related support.
- Attendance reporting.

The system investigation supported the inclusion of attendance reporting, time tracking, and employee management functionality.

---

# 18. Research Findings

FYP1 used two primary data-gathering methods:

```text
Questionnaire
+
Interview
```

## Questionnaire

The questionnaire was conducted through Google Forms.

- 15 closed-ended questions.
- 52 valid responses.
- Respondents included students, part-time employees, full-time employees, managers, supervisors, F&B workers, and potential future users.
- The questionnaire was open more generally because the client organization has only a small workforce.

Important findings included strong support for:

- Addressing attendance fraud.
- Improving attendance accuracy.
- Biometric attendance verification.
- Improved attendance monitoring.
- Employee scheduling functionality.

The findings supported the proposed facial verification, liveness detection, attendance management, and scheduling features.

---

## Interview

The interview was conducted with:

**Ms. Rashidah Binti Mohamad Salin**  
**Position:** Store Manager  
**Organization:** Subway Metro Point Complex

Important findings included:

- The client has a relatively small workforce.
- Attendance is currently associated with employee ID and the POS process.
- Attendance is monitored by management.
- Buddy punching and inaccurate attendance records are relevant concerns.
- Employees may forget to clock in or clock out.
- Attendance monitoring is a management challenge.
- Employee schedules are prepared using Microsoft Excel.
- Schedule management is performed manually by managers.
- Face ID / facial recognition was identified as an important feature.
- Management requires attendance-related reports.
- Employees require access to schedules, attendance history, notifications, and working-hour information.

These findings form the client-specific basis for SASMS requirements.

---

# 19. Agile Development Methodology

The project follows the **Agile methodology**.

The five phases used in the project are:

```text
Planning
   ↓
Design
   ↓
Development
   ↓
Testing
   ↓
Deployment & Review
   ↺
Feedback / Improvement
```

## Phase 1 – Planning

Activities include:

- Identify attendance and scheduling problems.
- Conduct client interview and questionnaire.
- Review existing systems and academic literature.
- Determine requirements and project scope.

## Phase 2 – Design

Activities include:

- Design system architecture.
- Design database structure.
- Create use case diagrams.
- Create employee and manager flowcharts.
- Design system interfaces.
- Define attendance, scheduling, RBAC, and reporting workflows.

## Phase 3 – Development

Develop modules incrementally, including:

- Authentication.
- RBAC.
- Employee management.
- Attendance management.
- Facial verification.
- Liveness detection.
- Shift scheduling.
- Reporting.
- Notifications.

## Phase 4 – Testing

Testing should include:

- Unit testing.
- Integration testing.
- System testing.
- Security testing.
- User Acceptance Testing (UAT).

Critical security-related tests should cover:

- Unauthorized role access.
- Attendance verification.
- Facial recognition.
- Liveness detection.
- Authentication.
- Session handling.

## Phase 5 – Deployment & Review

The completed system will be demonstrated to Subway Metro Point Complex.

Feedback should be collected from the client and used to refine system functionality.

---

# 20. Current Development Status

## Completed – FYP1

- Problem identification
- Project objectives
- Project scope
- Product scope
- Literature review
- Existing-system investigation
- Existing-system comparison
- Questionnaire
- Questionnaire analysis
- Client interview
- Interview analysis
- Functional requirements
- Software requirements
- Hardware requirements
- Use Case Diagram
- Employee Flowchart
- Manager/PIC Flowchart
- Agile methodology planning

## FYP2 – To Be Developed

- Project structure
- Database implementation
- Authentication
- RBAC
- Employee management
- Attendance module
- Facial verification
- Liveness detection
- Shift scheduling
- Attendance reports
- Notifications
- User interfaces
- System integration
- Testing
- UAT
- Deployment/demo

---

# 21. Development Priority

Recommended implementation order:

```text
1. Project Setup
        ↓
2. Database
        ↓
3. Authentication
        ↓
4. RBAC
        ↓
5. Employee Management
        ↓
6. Shift Scheduling
        ↓
7. Basic Attendance
        ↓
8. Python/OpenCV Integration
        ↓
9. Facial Verification
        ↓
10. Liveness Detection
        ↓
11. Reporting
        ↓
12. Notifications
        ↓
13. Security Hardening
        ↓
14. Testing / UAT
```

Do not start with facial recognition before the core employee, authentication, database, and attendance structures are functional.

---

# 22. Important Constraints for Development

When continuing this project:

1. **Do not change the established technology stack without discussing it first.**
2. ASP.NET Core MVC remains the primary web framework.
3. MySQL remains the database.
4. Python + OpenCV are planned for facial verification and basic liveness detection.
5. The system must remain web-based.
6. Employee and Manager/PIC are the primary roles.
7. RBAC must be enforced server-side.
8. Facial verification is required for attendance.
9. Liveness detection is a core security requirement.
10. Shift scheduling is a core project objective, not an optional feature.
11. Attendance reports are required for management.
12. The system is specifically designed around the requirements gathered from Subway Metro Point Complex.
13. Do not silently introduce major features that are outside the FYP1 scope.
14. Security features should be explainable and demonstrable during FYP2 evaluation.

---

# 23. Instructions for Claude / Coding Assistant

You are assisting with the implementation of a cybersecurity Final Year Project called **Secure Attendance and Shift Management System (SASMS)**.

Treat this README as the established FYP1 project baseline.

When helping with implementation:

- Preserve the defined project scope.
- Do not replace the technology stack without explicit approval.
- Explain implementation decisions before making major architectural changes.
- Prioritize working, demonstrable functionality suitable for a university FYP.
- Keep the architecture understandable enough for the student to explain during evaluation.
- Avoid unnecessary enterprise-level complexity.
- Apply secure coding practices throughout the application.
- Maintain clear separation between Employee and Manager/PIC permissions.
- Design the database before implementing dependent modules.
- Keep facial recognition and liveness detection modular so that the Python/OpenCV component can integrate with the ASP.NET Core MVC application.
- Ensure every implemented feature can be traced back to a project requirement or objective.
- When suggesting new features, clearly mark them as optional rather than silently adding them to the official scope.

Before generating large amounts of code, first inspect the existing project structure and determine what has already been implemented.

If starting from scratch, begin with:

1. ASP.NET Core MVC project structure.
2. MySQL database schema.
3. User authentication.
4. Employee/Manager-PIC RBAC.
5. Core employee and attendance models.

Then continue module-by-module according to the development priority above.

---

# 24. Core Project Principle

The purpose of SASMS is not simply to create another attendance application.

The project combines:

**Attendance Management + Facial Verification + Liveness Detection + RBAC + Shift Management**

to provide a more secure and manageable employee attendance process for Subway Metro Point Complex.