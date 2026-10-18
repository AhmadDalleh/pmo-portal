using Microsoft.AspNetCore.Identity;
using Npgsql;
using Pmo.Infrastructure.Identity;

namespace DataGenerator.Import;

/// <summary>
/// Creates the Identity roles and the demo users. Every user gets the password from DEMO_USER_PASSWORD,
/// hashed with the same PasswordHasher ASP.NET Core Identity uses at login. The password is never written to a file.
/// </summary>
public static class DemoUserWriter
{
    private static readonly string[] Roles = ["Administrator", "ProjectManager", "Contributor"];

    public static async Task WriteAsync(NpgsqlConnection connection, string usersCsvPath, string password)
    {
        var roleIds = new Dictionary<string, int>();
        foreach (var role in Roles)
        {
            await using var insertRole = new NpgsqlCommand(
                "INSERT INTO roles (name, normalized_name, concurrency_stamp) VALUES (@name, @normalized, @stamp) RETURNING id", connection);
            insertRole.Parameters.AddWithValue("name", role);
            insertRole.Parameters.AddWithValue("normalized", role.ToUpperInvariant());
            insertRole.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString());
            roleIds[role] = (int)(await insertRole.ExecuteScalarAsync())!;
        }

        var hasher = new PasswordHasher<AppUser>();
        foreach (var line in File.ReadLines(usersCsvPath).Skip(1))
        {
            // columns: id, email, full_name, department_id, job_title, is_active, role
            var c = CsvLine.Split(line);
            var user = new AppUser { Email = c[1] };

            await using var insertUser = new NpgsqlCommand(
                """
                INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash,
                                   security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled,
                                   access_failed_count, full_name, department_id, job_title, is_active)
                VALUES (@id, @email, @normalized, @email, @normalized, true, @hash, @security, @concurrency, false, false, true,
                        0, @full_name, @department, @job_title, @is_active)
                """, connection);
            insertUser.Parameters.AddWithValue("id", int.Parse(c[0]));
            insertUser.Parameters.AddWithValue("email", c[1]);
            insertUser.Parameters.AddWithValue("normalized", c[1].ToUpperInvariant());
            insertUser.Parameters.AddWithValue("hash", hasher.HashPassword(user, password));
            insertUser.Parameters.AddWithValue("security", Guid.NewGuid().ToString("N"));
            insertUser.Parameters.AddWithValue("concurrency", Guid.NewGuid().ToString());
            insertUser.Parameters.AddWithValue("full_name", c[2]);
            insertUser.Parameters.AddWithValue("department", int.Parse(c[3]));
            insertUser.Parameters.AddWithValue("job_title", c[4]);
            insertUser.Parameters.AddWithValue("is_active", bool.Parse(c[5]));
            await insertUser.ExecuteNonQueryAsync();

            await using var insertLink = new NpgsqlCommand("INSERT INTO user_roles (user_id, role_id) VALUES (@user, @role)", connection);
            insertLink.Parameters.AddWithValue("user", int.Parse(c[0]));
            insertLink.Parameters.AddWithValue("role", roleIds[c[6]]);
            await insertLink.ExecuteNonQueryAsync();
        }
    }
}
