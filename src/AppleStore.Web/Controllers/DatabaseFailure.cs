using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Controllers;

public static class DatabaseFailure
{
    // The one test for "the database refused this", used by every action that
    // turns such a failure into a message instead of an error page.
    public static bool IsDatabaseFailure(this Exception ex) => ex is DbUpdateException or DbException;
}
