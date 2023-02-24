﻿# HA Agent

A service which collects device data for Home Assistant.

## Usage

```
dotnet run -- [options]
```
```
HAAgent [options]
```

## Options

- `--config <config>`

  Path to configuration file [] (required).

- `--verbose`

  Display more details about what's going on.

- `--dry-run`

  Do not perform any actions, only pretend.

- `--once`

  Run data collection once only.

## Configuration

* `homeassistant` (object) Configuration for Home Assistant MQTT discovery
  * `server` (string) Hostname or IP
  * `port` (string, optional) Port number (default value: `1883`)
  * `username` (string, optional) Username
  * `password` (string, optional) Password
  * `prefix` (string, optional) Topic prefix (default value: `homeassistant`)
* `agents` (object) 
  * `system` (object, optional) Configuration for a system agent (key name is not used)
    * `type` (string) `system`
    * `name` (string, optional) Display name for this device in Home Assistant (default value: current hostname)

## Example configuration

```json
{
  "homeassistant": {
    "server": "homeassistant",
    "username": "homeassistant",
    "password": "homeassistant"
  },
  "agents": {
    "system": {
      "type": "system",
      "name": "My Computer"
    }
  }
}
```
