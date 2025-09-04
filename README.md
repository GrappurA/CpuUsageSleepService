# AutoSleepService

**AutoSleepService** is a small Windows application that automatically puts your PC to sleep after a specified amount of time **if the CPU usage is below a set threshold**. The service runs silently in the background without showing any console window.

---

## Features

- Monitors CPU usage in the background.
- Puts the PC to sleep after a configurable idle time.
- Runs silently on logon (can be set up via Task Scheduler).
- Lightweight and easy to install.

---

## Getting Started

### **Option 1: Install via Installer (Recommended)**

1. Go to the [Releases](https://github.com/GrappurA/CpuUsageSleepService/releases/) tab.
2. Download the latest installer (`AutoSleepServiceSetup.exe`).
3. Run the installer and follow the instructions.
4. The app will be ready to run silently on Windows startup.
5. Possibly configure it via CpuUsageConfig.json located at C:\ProgramData\CpuUsageSleep

---

### **Option 2: Build from Source**

If you want to compile it yourself:

1. Clone the repository:

```bash
git clone https://github.com/YourUsername/AutoSleepService.git
