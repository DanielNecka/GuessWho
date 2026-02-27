GuessWho

A simple desktop application project built with C# and WPF (.NET).

This repository currently contains the foundational structure of a WPF application intended to become a "Guess Who" style game.

📌 Project Description

GuessWho is a Windows desktop application created using Windows Presentation Foundation (WPF) in C#.

At its current stage, the project provides:

The base WPF application structure

A main window definition

Standard .NET project configuration

It serves as a starting template for further development of a "Guess Who" game.

🛠 Technologies Used

C#

.NET

WPF (Windows Presentation Foundation)

Visual Studio

📁 Project Structure
GuessWho/
│
├── GuessWho.csproj        # .NET project file
├── MainWindow.xaml        # UI layout definition
├── MainWindow.xaml.cs     # Code-behind for main window
├── App.xaml               # Application configuration
├── App.xaml.cs            # Application startup logic
└── GuessWho.sln           # Visual Studio solution file
Key Components

MainWindow.xaml – Defines the graphical user interface.

MainWindow.xaml.cs – Contains the logic associated with the main window.

InitializeComponent() – Loads and initializes UI components defined in XAML.

▶️ How to Run

Open GuessWho.sln in Visual Studio.

Make sure the .NET Desktop Development workload is installed.

Press F5 to run with debugging
or
Ctrl + F5 to run without debugging.

Requirements

Windows OS

Visual Studio 2022 or newer

Compatible .NET SDK version (as specified in the .csproj file)
