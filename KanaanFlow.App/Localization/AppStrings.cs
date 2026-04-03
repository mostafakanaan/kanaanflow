namespace KanaanFlow.App.Localization;

using System.Collections.Generic;

internal static class AppStrings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            // Shell
            ["TabDashboard"] = "Dashboard",
            ["TabTransactions"] = "Transactions",
            ["TabLoans"] = "Loans",
            ["TabReports"] = "Reports",
            ["TabSettings"] = "Settings",

            // Dashboard
            ["TodaySummary"] = "Today's Summary",
            ["Income"] = "Income",
            ["Expense"] = "Expense",
            ["Balance"] = "Balance",
            ["RecentTransactions"] = "Recent Transactions",

            // Transaction List
            ["FilterByDate"] = "Filter by Date",
            ["ApplyFilter"] = "Apply Filter",
            ["Delete"] = "Delete",
            ["AddTransactionBtn"] = "+ Add Transaction",

            // Add Transaction
            ["Amount"] = "Amount",
            ["Description"] = "Description",
            ["DescriptionPlaceholder"] = "What was this for?",
            ["Type"] = "Type",
            ["Category"] = "Category",
            ["Date"] = "Date",
            ["CurrencyLabel"] = "Currency",
            ["SaveTransaction"] = "Save Transaction",
            ["SelectCategory"] = "Please select a category.",
            ["AmountGreaterThanZero"] = "Amount must be greater than 0.",
            ["AddTransaction"] = "Add Transaction",

            // Loan List
            ["Active"] = "Active",
            ["Overdue"] = "Overdue",
            ["PaidOff"] = "Paid Off",
            ["AddLoanBtn"] = "+ Add Loan",

            // Add Loan
            ["ContactName"] = "Contact Name",
            ["ContactPlaceholder"] = "Who is this with?",
            ["Direction"] = "Direction",
            ["DueDateOptional"] = "Due Date (Optional)",
            ["SetDueDate"] = "Set due date",
            ["SaveLoan"] = "Save Loan",
            ["ContactRequired"] = "Contact name is required.",
            ["AddLoan"] = "Add Loan",

            // Reports
            ["Period"] = "Period",
            ["Generate"] = "Generate",
            ["Daily"] = "Daily",
            ["Weekly"] = "Weekly",
            ["Monthly"] = "Monthly",
            ["Reports"] = "Reports",

            // Settings
            ["License"] = "License",
            ["AppInfo"] = "App Info",
            ["Version"] = "Version",
            ["App"] = "App",
            ["Author"] = "Author",
            ["Language"] = "Language",
            ["Settings"] = "Settings",

            // License Page
            ["EnterLicenseKey"] = "Enter license key",
            ["PasteAndSave"] = "Paste the key and tap Save.",
            ["Save"] = "Save",

            // Welcome Page
            ["Welcome"] = "Welcome",

            // Categories
            ["Categories"] = "Categories",
            ["AddCategory"] = "Add Category",
            ["Name"] = "Name",
            ["IconPlaceholder"] = "Icon emoji (e.g. 🏠)",
            ["ColorPlaceholder"] = "Color hex (e.g. #FF5722)",
            ["Add"] = "Add",
            ["CategoryRequired"] = "Category name is required.",

            // Loans
            ["Loans"] = "Loans",
        },
        ["ar"] = new()
        {
            // Shell
            ["TabDashboard"] = "لوحة المعلومات",
            ["TabTransactions"] = "المعاملات",
            ["TabLoans"] = "القروض",
            ["TabReports"] = "التقارير",
            ["TabSettings"] = "الإعدادات",

            // Dashboard
            ["TodaySummary"] = "ملخص اليوم",
            ["Income"] = "الدخل",
            ["Expense"] = "المصاريف",
            ["Balance"] = "الرصيد",
            ["RecentTransactions"] = "المعاملات الأخيرة",

            // Transaction List
            ["FilterByDate"] = "تصفية حسب التاريخ",
            ["ApplyFilter"] = "تطبيق",
            ["Delete"] = "حذف",
            ["AddTransactionBtn"] = "+ إضافة معاملة",

            // Add Transaction
            ["Amount"] = "المبلغ",
            ["Description"] = "الوصف",
            ["DescriptionPlaceholder"] = "ما الغرض من هذا؟",
            ["Type"] = "النوع",
            ["Category"] = "الفئة",
            ["Date"] = "التاريخ",
            ["CurrencyLabel"] = "العملة",
            ["SaveTransaction"] = "حفظ المعاملة",
            ["SelectCategory"] = "يرجى اختيار فئة.",
            ["AmountGreaterThanZero"] = "يجب أن يكون المبلغ أكبر من 0.",
            ["AddTransaction"] = "إضافة معاملة",

            // Loan List
            ["Active"] = "نشط",
            ["Overdue"] = "متأخر",
            ["PaidOff"] = "مسدد",
            ["AddLoanBtn"] = "+ إضافة قرض",

            // Add Loan
            ["ContactName"] = "اسم الشخص",
            ["ContactPlaceholder"] = "مع من هذا القرض؟",
            ["Direction"] = "الاتجاه",
            ["DueDateOptional"] = "تاريخ الاستحقاق (اختياري)",
            ["SetDueDate"] = "تحديد تاريخ الاستحقاق",
            ["SaveLoan"] = "حفظ القرض",
            ["ContactRequired"] = "اسم الشخص مطلوب.",
            ["AddLoan"] = "إضافة قرض",

            // Reports
            ["Period"] = "الفترة",
            ["Generate"] = "إنشاء",
            ["Daily"] = "يومي",
            ["Weekly"] = "أسبوعي",
            ["Monthly"] = "شهري",
            ["Reports"] = "التقارير",

            // Settings
            ["License"] = "الترخيص",
            ["AppInfo"] = "معلومات التطبيق",
            ["Version"] = "الإصدار",
            ["App"] = "التطبيق",
            ["Author"] = "المؤلف",
            ["Language"] = "اللغة",
            ["Settings"] = "الإعدادات",

            // License Page
            ["EnterLicenseKey"] = "أدخل مفتاح الترخيص",
            ["PasteAndSave"] = "الصق المفتاح واضغط حفظ.",
            ["Save"] = "حفظ",

            // Welcome Page
            ["Welcome"] = "مرحباً",

            // Categories
            ["Categories"] = "الفئات",
            ["AddCategory"] = "إضافة فئة",
            ["Name"] = "الاسم",
            ["IconPlaceholder"] = "إيموجي الأيقونة (مثال: 🏠)",
            ["ColorPlaceholder"] = "كود اللون (مثال: #FF5722)",
            ["Add"] = "إضافة",
            ["CategoryRequired"] = "اسم الفئة مطلوب.",

            // Loans
            ["Loans"] = "القروض",
        }
    };

    public static string Get(string key, string language)
    {
        if (Translations.TryGetValue(language, out Dictionary<string, string>? dict) &&
            dict.TryGetValue(key, out string? value))
        {
            return value;
        }

        if (Translations.TryGetValue("en", out Dictionary<string, string>? fallback) &&
            fallback.TryGetValue(key, out string? fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }
}
