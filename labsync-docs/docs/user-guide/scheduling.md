---
sidebar_position: 4
---

# Scheduling Guide

LabSync scheduling enables automated execution of saved scripts on a recurring timetable.

## Scheduling Concepts

### Scheduled Script

A scheduled script associates a saved script with a target device group and a CRON timetable.

### Schedule Target

- A single device group
- A set of devices defined by the group
- Scheduled tasks are executed on every device in that group

### Status

- **Enabled:** The schedule is active
- **Disabled:** The schedule is stored but does not execute

## Creating a Schedule

### Steps

1. Navigate to **Scripts** → **Schedules**.
2. Click **New Schedule**.
3. Select the saved script.
4. Choose the target device group.
5. Enter a CRON expression.
6. Set the schedule to **Enabled**.
7. Click **Create**.

### Example Schedule Expressions

| Expression    | Meaning               |
| ------------- | --------------------- |
| `0 9 * * MON` | Every Monday at 09:00 |
| `0 0 * * *`   | Every day at midnight |
| `0 */6 * * *` | Every 6 hours         |
| `15 18 * * 5` | Every Friday at 18:15 |

### CRON Reference

- `minute` (0-59)
- `hour` (0-23)
- `day of month` (1-31)
- `month` (1-12)
- `day of week` (0-6, Sunday = 0 or 7)

## Managing Schedules

### Edit a Schedule

1. Open **Scripts** → **Schedules**.
2. Click the schedule.
3. Update script, group, or CRON expression.
4. Click **Save**.

### Enable or Disable

1. Open the schedule.
2. Toggle the **Status** switch.
3. Changes take effect immediately.

### Delete a Schedule

1. Open the schedule.
2. Click **Delete**.
3. Confirm deletion.

## Schedule Execution

### How It Works

- The scheduler component evaluates CRON expressions.
- When a schedule triggers, a job is created for each target device.
- Devices execute the saved script and report results.

### Execution Results

- Job status appears in the **Jobs** view.
- Successful jobs show output and exit code 0.
- Failed jobs include error output and non-zero exit code.

## Execution History

Each scheduled execution stores:

- timestamp
- target device group
- script title
- success/failure status
- output log
- duration

### Reviewing History

1. Open **Scripts** → **Schedules**.
2. Select a schedule.
3. View the history section.
4. Click an entry for detailed output.

## Use Cases

### Regular Maintenance

- Clean temporary files nightly
- Verify disk usage every morning
- Rotate logs weekly

### Compliance Checks

- Validate installed software versions
- Collect security patch status
- Audit configuration changes

### Educational Labs

- Reset lab machines daily
- Deploy test data before class
- Collect results after student assignments

## Best Practices

### Schedule Design

- Use descriptive schedule names.
- Keep CRON expressions simple.
- Test the script manually before scheduling.
- Use group-specific schedules for different environments.

### Resource Management

- Avoid running heavy scripts during peak usage.
- Stagger schedules across multiple device groups.
- Monitor execution history for long-running jobs.

### Security

- Use scripts that follow least privilege principles.
- Avoid storing secrets directly in scripts.
- Use SSH credentials only when needed.

## Troubleshooting

### Schedule Does Not Run

- Verify the schedule is enabled.
- Check that the target group contains online devices.
- Confirm the CRON expression is valid.
- Review scheduler logs on the server.

### Job Fails for All Devices

- Inspect the script for errors.
- Validate interpreter compatibility.
- Confirm the target devices are approved and online.
- Check output for permission errors.

---

Next: [Script Execution Guide](./script-execution) or [Device Management](./device-management)
