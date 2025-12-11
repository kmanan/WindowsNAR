# Network Adapter Restarter

A simple Windows application that displays all network adapters and allows you to restart any adapter with a single click. Because someone in the Windows team thought that a perfectly great network adapter reset troubleshooter had to die.

![App Icon](icon.ico)

## Features

- **View All Adapters**: Lists all network adapters on your system
- **One-Click Restart**: Disable and re-enable adapters instantly
- **Administrator Access**: Runs with elevated privileges automatically
- **Clean Interface**: Simple, straightforward UI

## Download

Download the latest version from the [Releases page](https://github.com/yourusername/NetworkAdapterRestarter/releases/latest).

<img width="486" height="413" alt="image" src="https://github.com/user-attachments/assets/a7a97a62-1691-4dc4-84b9-d443cea19dff" />

## Installation

1. Download `NetworkAdapterRestarter.exe` from the latest release
2. Run the executable - it will request administrator privileges automatically
3. No installation required!

## Usage

1. Launch the application (requires administrator rights)
2. The app will display all network adapters
3. Select an adapter from the list
4. Click "Restart Selected Adapter"
5. Confirm the restart
6. The adapter will be disabled then re-enabled (takes ~1-2 seconds)

## Requirements

- **Operating System**: Windows 10 or Windows 11
- **Permissions**: Administrator privileges
- **.NET Runtime**: Not required (self-contained executable)

## How It Works

The application uses Windows `netsh` commands:
- `netsh interface show interface` - Lists all network adapters
- `netsh interface set interface "name" disable` - Disables an adapter
- `netsh interface set interface "name" enable` - Enables an adapter

## Building from Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/NetworkAdapterRestarter.git
   cd NetworkAdapterRestarter
   ```

2. Build the project:
   ```bash
   dotnet build -c Release
   ```

3. Create single-file executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
   ```

4. Executable location: `bin\Release\net6.0-windows\win-x64\publish\NetworkAdapterRestarter.exe`

## Why Use This?

Network connectivity issues are common and often resolved by simply restarting the network adapter. This tool makes that process instant instead of:
1. Opening Control Panel or Settings
2. Navigating to Network Adapters
3. Right-clicking the adapter
4. Clicking Disable, waiting, then clicking Enable

## Troubleshooting

**"Access Denied" Error**: Make sure you're running the application as Administrator. Right-click the .exe and select "Run as administrator" if it doesn't prompt automatically.

**Adapter doesn't appear**: Click the "Refresh List" button to reload adapters.

**Connection doesn't restore**: Some adapters may require additional time. Wait 5-10 seconds and check your connection.

## Contributing

Contributions are welcome! Feel free to:
- Report bugs via [Issues](https://github.com/yourusername/NetworkAdapterRestarter/issues)
- Submit pull requests
- Suggest new features

## License

This project is open source and available under the [MIT License](LICENSE).

## Credits

Created to simplify network troubleshooting on Windows systems.

---

**Note**: Always be cautious when running applications that require administrator privileges. Review the source code if you have concerns.
