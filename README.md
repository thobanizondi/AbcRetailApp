# AbcRetailApp - Cloud-Based Retail Management System

AbcRetailApp is a retail management web application built with **ASP.NET MVC and C#**, designed to support common retail operations such as product management, inventory tracking, customer order processing, and sales monitoring.

The project explores the development of a database-driven web application using Microsoft technologies, with Azure services for cloud hosting and data storage.

## Features

- **Product Catalogue:** Manage and display product information.
- **Inventory Management:** Support product and stock record management.
- **Order Processing:** Handle customer orders and maintain order information.
- **User Authentication:** Support authenticated access to application features.
- **Role-Based Access:** Organise access to functionality according to user roles, where configured.
- **Database Integration:** Use Entity Framework to interact with Azure SQL Database.
- **Cloud Deployment:** Designed for hosting through Microsoft Azure App Services.

## Technology Stack

| Technology | Purpose |
|---|---|
| C# | Application logic |
| ASP.NET MVC | Web application framework |
| Entity Framework | Database access and object-relational mapping |
| Azure App Services | Cloud application hosting |
| Azure SQL Database | Relational data storage |
| Visual Studio | Development and debugging |

## Getting Started

### Prerequisites

- Visual Studio with the required ASP.NET and web development components
- The .NET Framework or .NET version targeted by the project
- Access to a compatible SQL Server database
- An Azure subscription if deploying to Azure

### Installation

Clone the repository:

```bash
git clone https://github.com/thobanizondi/AbcRetailApp.git
```

Navigate to the project directory:

```bash
cd AbcRetailApp
```

Then:

1. Open the solution file in Visual Studio.
2. Restore the required NuGet packages.
3. Configure the database connection string for your development environment.
4. Apply any required Entity Framework migrations or database scripts.
5. Build and run the application locally.
6. Configure Azure App Services and Azure SQL Database if deploying to the cloud.

**Configuration note:** Exact setup steps may vary depending on the project's .NET version, database configuration, and migration setup.

## Cloud Deployment

The application is designed to use Microsoft Azure services:

- **Azure App Services** to host the web application.
- **Azure SQL Database** to store retail and order data.

For deployment, configure the production database connection securely using Azure application settings or an appropriate secrets-management solution. Do not commit credentials or connection strings containing passwords to the repository.

## Potential Future Enhancements

- Low-stock alerts and inventory reporting
- Search, filtering, and sorting for products and orders
- Sales dashboards and performance analytics
- Order status tracking and customer notifications
- Improved input validation and error handling
- Automated unit and integration testing
- CI/CD deployment using GitHub Actions or Azure DevOps
- Application monitoring and structured logging

## Project Purpose

AbcRetailApp demonstrates the design of a database-driven retail web application using C# and ASP.NET MVC, with Entity Framework for data access and Microsoft Azure services for cloud deployment and storage.

## Author

**Thobani Zondi**

Software Development | SQL and Database Development | Microsoft Azure

GitHub: [thobanizondi](https://github.com/thobanizondi)
