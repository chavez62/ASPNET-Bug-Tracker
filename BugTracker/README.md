# 🐛 Bug Tracker

A robust, secure, and user-friendly bug tracking system built with ASP.NET Core, designed to help development teams efficiently manage and track software issues.

## ✨ Features

- **👥 User Authentication & Authorization**
  - Role-based access control (Admin and User roles)
  - Secure user registration and login
  - Email confirmation functionality

- **🎯 Bug Management**
  - Create, read, update, and delete bug reports
  - Rich text description support
  - Customizable bug status (Open, In Progress, Under Review, Resolved, Closed)
  - Severity levels (Low, Medium, High, Critical)
  - File attachments with secure handling
  - Activity logging and commenting system

- **📊 Dashboard & Analytics**
  - Real-time statistics and metrics
  - Visual representations using charts
  - Bug distribution by status and severity
  - Activity timeline

- **🔍 Search & Filter Capabilities**
  - Advanced search functionality
  - Multiple filter options
  - Sorting and pagination
  - Export capabilities

- **🔒 Security Features**
  - CSRF protection
  - XSS prevention
  - Secure file handling
  - Input validation
  - SQL injection prevention

## 🛠️ Technical Stack

- **⚙️ Backend**
  - ASP.NET Core 6.0
  - Entity Framework Core
  - Identity Framework
  - SQLite Database

- **🎨 Frontend**
  - Bootstrap 5
  - jQuery
  - Chart.js for visualizations
  - Bootstrap Icons

## 📋 Prerequisites

- .NET 6.0 SDK or later
- Visual Studio 2022 or Visual Studio Code
- SQLite (included in the project)

## 🚀 Getting Started

1. **📥 Clone the Repository**
   ```bash
   git clone https://github.com/yourusername/bug-tracker.git
   cd bug-tracker
   ```

2. **⚙️ Setup Configuration**
   - Update `appsettings.json` with your settings
   - Configure email settings for notifications
   - Set up file storage path

3. **🗄️ Initialize Database**
   ```bash
   dotnet ef database update
   ```

4. **▶️ Run the Application**
   ```bash
   dotnet run
   ```
   Or open the solution in Visual Studio and press F5

5. **👤 Default Admin Account**
   - Email: admin@bugtracker.com
   - Password: Admin123!

## 📁 Project Structure

```
BugTracker/
├── Controllers/          # MVC Controllers
├── Models/              # Data models and ViewModels
├── Views/               # Razor views
├── Services/            # Business logic and services
├── Data/                # Database context and migrations
└── wwwroot/            # Static files (CSS, JS, images)
```

## 🔐 Security Considerations

- Implements secure file upload handling with:
  - File type validation
  - Size restrictions
  - Antivirus scanning capability
  - Secure storage
- CSRF protection on all forms
- Proper authorization checks
- Input sanitization
- Secure password requirements

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 👏 Acknowledgements

- Bootstrap for the UI framework
- Chart.js for visual
