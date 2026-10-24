# User-Friendly Error System

Users should never be forced to interpret stack traces, exception class names, HTTP codes, OS error numbers, or internal paths.

## Error anatomy
**Title:** plain-language problem  
**Explanation:** what happened in context  
**Recovery:** best next action  
**Alternatives:** retry/change location/reconnect/request permission/etc.  
**Technical details:** optional disclosure only when useful for support/debugging

## Examples

Instead of:
`IOException: EACCES /storage/...`

Use:
`We couldn't access this folder.`
`File Organizer doesn't currently have permission to use it. Choose another folder or grant access.`

Instead of:
`ENOSPC`

Use:
`Not enough storage space.`
`Free up space or choose another destination, then try again.`

Instead of:
`DeviceNotFoundException`

Use:
`Your device was disconnected.`
`Reconnect it, then retry the operation.`

Never blame the user. Never expose secrets/tokens in technical details.
