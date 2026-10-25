namespace KanaanFlow.Core.Finance;

using System.Collections.Generic;

/// <summary>
/// The categories every new book starts with. Their ids are fixed so every device agrees on them.
/// </summary>
public static class DefaultCategories
{
    public static List<PeopleCategory> People()
    {
        return new List<PeopleCategory>
        {
            new PeopleCategory { Id = "customer", NameAr = "زبون", NameEn = "Customer" },
            new PeopleCategory { Id = "supplier", NameAr = "مورد", NameEn = "Supplier" },
            new PeopleCategory { Id = "partner", NameAr = "شريك", NameEn = "Partner" },
            new PeopleCategory { Id = "employee", NameAr = "موظف", NameEn = "Employee" },
            new PeopleCategory { Id = "family", NameAr = "أقارب", NameEn = "Family" },
            new PeopleCategory { Id = "furqan", NameAr = "بيت الفرقان", NameEn = "Al-Furqan House" },
            new PeopleCategory { Id = PeopleCategory.OtherId, NameAr = "أخرى", NameEn = "Other" }
        };
    }

    public static List<EntryCategory> Entries()
    {
        return new List<EntryCategory>
        {
            new EntryCategory { Id = "sales", NameAr = "مبيعات", NameEn = "Sales", Kind = CategoryKind.In },
            new EntryCategory { Id = "yarn", NameAr = "شراء خيط", NameEn = "Yarn", Kind = CategoryKind.Out },
            new EntryCategory { Id = "dye", NameAr = "صباغة", NameEn = "Dyeing", Kind = CategoryKind.Out },
            new EntryCategory { Id = "wages", NameAr = "أجور", NameEn = "Wages", Kind = CategoryKind.Out },
            new EntryCategory { Id = "draw", NameAr = "مسحوبات", NameEn = "Withdrawals", Kind = CategoryKind.Out },
            new EntryCategory { Id = "rent", NameAr = "إيجار", NameEn = "Rent", Kind = CategoryKind.Out },
            new EntryCategory { Id = "transport", NameAr = "نقل وشحن", NameEn = "Shipping", Kind = CategoryKind.Out },
            new EntryCategory { Id = EntryCategory.LoanId, NameAr = "دين وسداد", NameEn = "Loan", Kind = CategoryKind.Both },
            new EntryCategory { Id = EntryCategory.OtherId, NameAr = "أخرى", NameEn = "Other", Kind = CategoryKind.Both }
        };
    }
}
