using Behind.Common.Modules;
using Behind.Contact.Common;
using Behind.Contact.Database.Aggregates;
using Behind.Contact.Domain;

namespace Behind.Contact.WebApi;

public static class ContactModule
{
    public static ModuleDescriptor Descriptor { get; } = new(ContactModuleNames.Key, ContactModuleNames.Name)
    {
        GrantAreas = [new ModuleGrantArea(ContactModuleNames.Area, ModuleAudience.Administration)],
        MenuItems = [new ModuleMenuItem(Guid.Parse("9d62eac2-e351-4a43-92d9-061a9901a341"), "Contact", "#/contact", 80)],
        AdministrationPages = [new ModuleAdministrationPage("contact.administration", "fa-envelope", "#/administration/contact", ContactModuleNames.ReadGrant)],
        Routes = [new ModuleRoute("#/contact", "modules/contact/controllers/contact"), new ModuleRoute("#/administration/contact", "modules/contact/controllers/contactmanager")],
        Widgets = [new ModuleWidget("contact.form", "contact.widget", "modules/contact/models/contact-widget")],
        Frontend = new ModuleFrontend(typeof(ContactModule).Assembly),
        Database = new ModuleDatabase(typeof(ContactFormConfiguration).Assembly)
        {
            SoftDeletedEntities = [typeof(ContactForm), typeof(ContactRequest)],
            ContentRestrictedEntities = [typeof(ContactForm)],
            ContentLocaleEditPolicies = new Dictionary<string, string> { [nameof(ContactForm)] = ContactModuleNames.WriteGrant }
        }
    };
}
