# GooseGooseGo .Net - .Net API Develop

This is the work in progress / testbed for  the .NET API which wil eventually be serving the **GooseGoosGo** Android app
This API currently connects to the Kraken and the Crypto.com APIs

## Project Overview

The aim is to get info from centralised exchanges such as Kraken and Crypto.com, to obtain asset information, to detect the movement in price information

The project is initially set up for use with Bootstrap.
Plans are afoot to include the use of React (yes on Windows)

There is also GooseGooseGo development underway in the Linux/Ubuntu environment using React / NextJS / Django

## Work in progress deployment

As development progresses, the public deployment is up at 
http://goosegoosego.t21.uk/


### Technologies Used

- **ASP.NET Core**: The web framework used to build the API.
- **Entity Framework Core**: ORM used for data access.
- **Sql Server**: The database technology used

## Getting Started


### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (8.0 or later)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- [SQL Server Management Studio (SSMS)](https://aka.ms/ssms)
   - Download and run the installer from the link above.
   - SSMS provides a graphical interface for managing SQL Server databases.
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Git for Windows](https://git-scm.com/download/win)
   - Download and run the installer from the link above.
   - Follow the setup wizard, leaving default options unless you have specific needs.
   - After installation, you can use Git from Command Prompt, PowerShell, or Git Bash.

### Setup 

On Windows (most suited) use command prompt

On MacOs or Linux use bash / terminal

1. **Clone the Repository**:
   git clone https://github.com/ExploseIT/GooseGooseGo_Net.git
   cd GooseGooseGo_Net

2. **Install node and npm**:
 From https://nodejs.org

3. **Build wwwroot/dist folder**:
  From node_modules by running 'npm install' then 'npx gulp'

4. **Create the ggg_net application pool in IIS**:
 Only if this is possible and you're in the Windows environment.
 Otherwise there will have to be workarounds.
 For this, locate and run the ggg_solution\ggg.sln\ggg_create\ggg_create.sql script in Sql Studio Management Studio
