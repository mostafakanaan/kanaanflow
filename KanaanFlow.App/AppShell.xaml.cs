namespace KanaanFlow.App;

using KanaanFlow.App.Pages;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("AddTransaction", typeof(AddTransactionPage));
		Routing.RegisterRoute("AddLoan", typeof(AddLoanPage));
		Routing.RegisterRoute("Categories", typeof(CategoryListPage));
	}
}
