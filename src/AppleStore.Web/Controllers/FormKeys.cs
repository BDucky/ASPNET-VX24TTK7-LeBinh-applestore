using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// The one way a staff form gets its key (stock receipts, counter sales): the
// key lives in the form's address, so Back returns to the same key. No key,
// or an empty one, gets a fresh key; a key already used leads to what it
// made instead of the form.
public static class FormKeys
{
    public static async Task<IActionResult?> FreshOrUsedAsync(this Controller controller, Guid? key, Func<Guid, Task<int?>> usedBy, Func<int, IActionResult> toUsed)
    {
        if (key is not { } formKey || formKey == Guid.Empty)
            return controller.RedirectToAction("New", new { key = Guid.NewGuid() });
        return await usedBy(formKey) is { } made ? toUsed(made) : null;
    }
}
