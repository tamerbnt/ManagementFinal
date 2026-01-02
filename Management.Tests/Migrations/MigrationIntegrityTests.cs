using System;
using System.IO;
using System.Threading.Tasks;
using FluentMigrator.Runner;
using Management.Infrastructure.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Management.Tests.Migrations
{
    public class MigrationIntegrityTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _connection;

        public MigrationIntegrityTests()
        {
            // Create in-memory SQLite database
            _connectionString = "Data Source=:memory:";
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }

        [Fact]
        public async Task Migration_001_AddsGenderColumn_WithoutDataLoss()
        {
            // Arrange: Create legacy Members table with test data
            await CreateLegacyMembersTable();
            await InsertLegacyTestData();

            // Act: Run migration
            RunMigration();

            // Assert: Verify Gender column exists and data preserved
            using (var command = _connection.CreateCommand())
            {
                // Check column exists
                command.CommandText = "PRAGMA table_info(Members)";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    bool genderColumnFound = false;
                    while (await reader.ReadAsync())
                    {
                        if (reader.GetString(1) == "Gender")
                        {
                            genderColumnFound = true;
                            break;
                        }
                    }
                    Assert.True(genderColumnFound, "Gender column should exist after migration");
                }

                // Verify data preserved
                command.CommandText = "SELECT COUNT(*) FROM Members";
                var count = (long)await command.ExecuteScalarAsync();
                Assert.Equal(2, count);

                // Verify specific data
                command.CommandText = "SELECT FullName FROM Members WHERE Email = 'john@test.com'";
                var name = (string)await command.ExecuteScalarAsync();
                Assert.Equal("John Doe", name);
            }
        }

        [Fact]
        public async Task Migration_001_CreatesFacilityConfigTable()
        {
            // Act: Run migration
            RunMigration();

            // Assert: Verify FacilityConfig table exists
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT name FROM sqlite_master 
                    WHERE type='table' AND name='FacilityConfig'";
                
                var tableName = (string)await command.ExecuteScalarAsync();
                Assert.Equal("FacilityConfig", tableName);

                // Verify LastReportSent column exists
                command.CommandText = "PRAGMA table_info(FacilityConfig)";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    bool lastReportSentFound = false;
                    while (await reader.ReadAsync())
                    {
                        if (reader.GetString(1) == "LastReportSent")
                        {
                            lastReportSentFound = true;
                            break;
                        }
                    }
                    Assert.True(lastReportSentFound, "LastReportSent column should exist");
                }
            }
        }

        [Fact]
        public void Migration_001_CanRollback_WithoutErrors()
        {
            // Arrange: Run migration up
            RunMigration();

            // Act: Rollback migration
            var serviceProvider = CreateMigrationServices();
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateDown(1);
            }

            // Assert: Verify Gender column removed
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(Members)";
                using (var reader = command.ExecuteReader())
                {
                    bool genderColumnFound = false;
                    while (reader.Read())
                    {
                        if (reader.GetString(1) == "Gender")
                        {
                            genderColumnFound = true;
                            break;
                        }
                    }
                    Assert.False(genderColumnFound, "Gender column should be removed after rollback");
                }
            }
        }

        private async Task CreateLegacyMembersTable()
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE Members (
                        Id TEXT PRIMARY KEY,
                        FacilityId TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Email TEXT,
                        Phone TEXT,
                        Status TEXT,
                        ExpirationDate TEXT,
                        CreatedAt TEXT
                    )";
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertLegacyTestData()
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO Members (Id, FacilityId, FullName, Email, Phone, Status, ExpirationDate, CreatedAt)
                    VALUES 
                        ('guid1', 'facility1', 'John Doe', 'john@test.com', '1234567890', 'Active', '2025-12-31', '2024-01-01'),
                        ('guid2', 'facility1', 'Jane Smith', 'jane@test.com', '0987654321', 'Active', '2025-12-31', '2024-01-01')";
                await command.ExecuteNonQueryAsync();
            }
        }

        private void RunMigration()
        {
            var serviceProvider = CreateMigrationServices();
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp();
            }
        }

        private IServiceProvider CreateMigrationServices()
        {
            return new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddSQLite()
                    .WithGlobalConnectionString(_connectionString)
                    .ScanIn(typeof(Migration_001_AddGenderAndConfig).Assembly).For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider(false);
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
