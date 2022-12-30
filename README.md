# HA Agent

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

* `mqtt` (object) configuration for MQTT server inside Home Assistant
  * `server` (string) hostname or IP
  * `port` (string, optional) port number (default value: `1883`)
  * `username` (string, optional) username
  * `password` (string, optional) password
* `homeassistant` (object, optional) 
  * `prefix` (string, optional) MQTT topic prefix (default value: `homeassistant`)
  * `deviceName` (string, optional) Display name for this device in Home Assistant (default value: current hostname)

## Example configuration

```json
{
  "mqtt": {
    "server": "homeassistant",
    "username": "homeassistant",
    "password": "homeassistant"
  },
  "homeassistant": {
    "deviceName": "My Computer"
  }
}
```
