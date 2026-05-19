---
sidebar_position: 1
---

# API Reference

Complete API documentation for LabSync Server endpoints.

## Authentication

All endpoints (except login and setup) require JWT authentication:

```
Authorization: Bearer <jwt_token>
```

**Token Duration:** 8 hours

**Obtaining a Token:**

```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "password"
}

Response:
{
  "token": "eyJhbGc...",
  "expiresIn": 28800
}
```

## System Endpoints

### Get System Status

```
GET /api/system/status
```

**Response (200):**

```json
{
  "setupComplete": true
}
```

**Use Case:** Check if initial setup is complete

### Setup System

```
POST /api/system/setup
Content-Type: application/json
```

**Request:**

```json
{
  "username": "admin",
  "password": "securepassword123"
}
```

**Response (200):**

```json
{
  "message": "Setup complete. You can now log in."
}
```

**Errors:**

- 409 Conflict: Setup already completed

## Agent Endpoints

### Register Agent

```
POST /api/agents/register
Content-Type: application/json
```

**Request:**

```json
{
  "hostname": "WORKSTATION-01",
  "macAddress": "00:11:22:33:44:55",
  "platform": "Windows",
  "osVersion": "Windows 10",
  "ipAddress": "192.168.1.100"
}
```

**Response (200):**

```json
{
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "token": "eyJhbGc...",
  "message": "Device is authorized."
}
```

**Note:** If device not approved, `token` is null.

## Device Endpoints

### List All Devices

```
GET /api/devices
Authorization: Bearer <token>
```

**Response (200):**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "hostname": "WORKSTATION-01",
    "ipAddress": "192.168.1.100",
    "macAddress": "00:11:22:33:44:55",
    "platform": "Windows",
    "osVersion": "Windows 10",
    "status": "Online",
    "isApproved": true,
    "isOnline": true,
    "registeredAt": "2026-05-19T10:30:00Z",
    "lastSeenAt": "2026-05-19T14:45:00Z",
    "groupId": null,
    "groupName": null
  }
]
```

### Approve Device

```
POST /api/devices/{deviceId}/approve
Authorization: Bearer <token>
```

**Response (200):**

```json
{
  "message": "Device approved successfully."
}
```

### Delete Device

```
DELETE /api/devices/{deviceId}
Authorization: Bearer <token>
```

**Response (200):**

```json
{
  "message": "Device deleted successfully."
}
```

### Create Job

```
POST /api/devices/{deviceId}/jobs
Authorization: Bearer <token>
Content-Type: application/json
```

**Request (Script Execution):**

```json
{
  "command": "ScriptExecution",
  "arguments": {
    "__InterpreterType": "PowerShell",
    "__ScriptContent": "Get-Date",
    "__TimeoutSeconds": "300"
  },
  "scriptPayload": null
}
```

**Request (Metrics Collection):**

```json
{
  "command": "CollectMetrics",
  "arguments": {},
  "scriptPayload": null
}
```

**Response (202 Accepted):**

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "command": "ScriptExecution",
  "arguments": "...",
  "status": "Pending",
  "exitCode": null,
  "output": null,
  "createdAt": "2026-05-19T14:45:00Z",
  "finishedAt": null
}
```

### Get Device Jobs

```
GET /api/devices/{deviceId}/jobs
Authorization: Bearer <token>
```

**Response (200):** Array of JobDto objects

### Get Job Details

```
GET /api/devices/{deviceId}/jobs/{jobId}
Authorization: Bearer <token>
```

**Response (200):**

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "deviceId": "550e8400-e29b-41d4-a716-446655440000",
  "command": "ScriptExecution",
  "status": "Completed",
  "exitCode": 0,
  "output": "5/19/2026 2:45:00 PM",
  "createdAt": "2026-05-19T14:45:00Z",
  "finishedAt": "2026-05-19T14:45:05Z"
}
```

### Assign Device to Group

```
POST /api/devices/{deviceId}/group
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:**

```json
{
  "groupId": "550e8400-e29b-41d4-a716-446655440001"
}
```

**Response (200):**

```json
{
  "message": "Device assigned to group."
}
```

### Remove Device from Group

```
DELETE /api/devices/{deviceId}/group
Authorization: Bearer <token>
```

**Response (200):**

```json
{
  "message": "Device removed from group."
}
```

### Set SSH Credentials

```
POST /api/devices/{deviceId}/credentials
Authorization: Bearer <token>
Content-Type: application/json
```

**Request (Password):**

```json
{
  "username": "ubuntu",
  "password": "my_password",
  "privateKey": null,
  "useKeyAuthentication": false
}
```

**Request (SSH Key):**

```json
{
  "username": "ubuntu",
  "password": null,
  "privateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
  "useKeyAuthentication": true
}
```

**Response (200):**

```json
{
  "message": "SSH Credentials saved successfully."
}
```

## Device Groups

### List All Groups

```
GET /api/device-groups
Authorization: Bearer <token>
```

**Response (200):**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "name": "Lab A",
    "description": "Computer Lab A - Room 101",
    "createdAt": "2026-05-19T10:00:00Z",
    "deviceCount": 25,
    "devices": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "hostname": "WORKSTATION-01",
        "isOnline": true
      }
    ]
  }
]
```

### Create Group

```
POST /api/device-groups
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:**

```json
{
  "name": "Lab A",
  "description": "Computer Lab A - Room 101"
}
```

**Response (201):** DeviceGroupDto

### Update Group

```
PUT /api/device-groups/{groupId}
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:**

```json
{
  "name": "Lab A (Updated)",
  "description": "Updated description"
}
```

**Response (200):** DeviceGroupDto

### Delete Group

```
DELETE /api/device-groups/{groupId}
Authorization: Bearer <token>
```

**Response (204 No Content)**

## Scripts

### List Scripts

```
GET /api/saved-scripts
Authorization: Bearer <token>
```

**Response (200):**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440002",
    "title": "System Health Check",
    "description": "Verify system resources",
    "content": "Get-ComputerInfo",
    "interpreter": "powershell",
    "createdAt": "2026-05-19T10:00:00Z",
    "updatedAt": "2026-05-19T10:00:00Z"
  }
]
```

### Create Script

```
POST /api/saved-scripts
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:**

```json
{
  "title": "System Health Check",
  "description": "Verify system resources",
  "content": "Get-ComputerInfo",
  "interpreter": "powershell"
}
```

**Interpreters:** `bash`, `powershell`, `cmd`

**Response (201):** SavedScriptDto

### Get Script

```
GET /api/saved-scripts/{scriptId}
Authorization: Bearer <token>
```

**Response (200):** SavedScriptDto

### Update Script

```
PUT /api/saved-scripts/{scriptId}
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:** Same as Create

**Response (200):** SavedScriptDto

### Delete Script

```
DELETE /api/saved-scripts/{scriptId}
Authorization: Bearer <token>
```

**Response (204 No Content)**

## Scheduled Scripts

### Create Schedule

```
POST /api/scheduled-scripts
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:**

```json
{
  "savedScriptId": "550e8400-e29b-41d4-a716-446655440002",
  "cronExpression": "0 9 * * MON",
  "deviceGroupId": "550e8400-e29b-41d4-a716-446655440001",
  "isEnabled": true
}
```

**CRON Format:** Standard cron expression

- `0 9 * * MON` - Every Monday at 9 AM
- `0 */2 * * *` - Every 2 hours

**Response (200):** ScheduledScriptDto

### List Schedules

```
GET /api/scheduled-scripts
Authorization: Bearer <token>
```

**Response (200):** Array of ScheduledScriptDto

### Get Schedule

```
GET /api/scheduled-scripts/{scheduleId}
Authorization: Bearer <token>
```

**Response (200):** ScheduledScriptDto

### Update Schedule

```
PUT /api/scheduled-scripts/{scheduleId}
Authorization: Bearer <token>
Content-Type: application/json
```

**Request:** Same as Create

**Response (200):** ScheduledScriptDto

### Delete Schedule

```
DELETE /api/scheduled-scripts/{scheduleId}
Authorization: Bearer <token>
```

**Response (204 No Content)**

## Error Responses

### 400 Bad Request

```json
{
  "message": "Invalid parameters"
}
```

### 401 Unauthorized

```json
{
  "message": "Missing or invalid JWT token"
}
```

### 403 Forbidden

```json
{
  "message": "Insufficient permissions"
}
```

### 404 Not Found

```json
{
  "message": "Resource not found"
}
```

### 409 Conflict

```json
{
  "message": "Device already exists"
}
```

### 500 Internal Server Error

```json
{
  "message": "Internal server error"
}
```

## Rate Limiting

- No built-in rate limiting (implement at reverse proxy)
- Recommended: 100 requests per minute per IP

## API Base URL

Development: `http://localhost:5038`
Production: `https://your-domain.com`
