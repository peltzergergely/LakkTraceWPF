using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for DatabaseWindow.xaml
    /// </summary>
    public partial class DatabaseWindow : Window
    {
        public DatabaseWindow()
        {
            InitializeComponent();
            SettingUpTheParameters();
        }

        private void SettingUpTheParameters()
        {

            // set back the short date pattern to dd-MM-yyyy after lacquer load (MM-yyyy)
            CultureInfo ci = CultureInfo.CreateSpecificCulture(CultureInfo.CurrentCulture.Name);
            ci.DateTimeFormat.ShortDatePattern = "dd-MM-yyyy";
            Thread.CurrentThread.CurrentCulture = ci;

            // Starting and Ending date
            startDate.SelectedDate = new DateTime(2017, 10, 01);
            endDate.SelectedDate = DateTime.Today;

            // Add items to product's combobox
            // how to use: (prodCbx.SelectedItem as ComboboxItem).Value.ToString()
            ComboboxItem BMWitem = new ComboboxItem("BMW", "bmw");
            prodCbx.Items.Add(BMWitem);

            ComboboxItem VolvoItem = new ComboboxItem("Volvo", "volvo");
            prodCbx.Items.Add(VolvoItem);

            ComboboxItem LakkItem = new ComboboxItem("Lakk", "lakk");
            prodCbx.Items.Add(LakkItem);

            prodCbx.SelectedIndex = 0;

            // Add items to workstation's combobox
            ComboboxItem oldWorkStationItem = new ComboboxItem("Régi állomás", "'old_lakk_pc'");
            workStationCbx.Items.Add(oldWorkStationItem);

            ComboboxItem newWorkStationItem = new ComboboxItem("Új állomás", "'DESKTOP-BVFFOIU'");
            workStationCbx.Items.Add(newWorkStationItem);

            ComboboxItem allWorkStationItem = new ComboboxItem("Összes", "'old_lakk_pc' or workstation LIKE '%' or workstation is null");
            //allWorkStationItem.Value = "'old_lakk_pc' or workstation LIKE '%' or workstation is null"; // list all workstation dynamically, if new workstation added to DB this will list it
            workStationCbx.Items.Add(allWorkStationItem);

            workStationCbx.SelectedIndex = 0;

        }

        public class ComboboxItem // My Combobox item because the name and key value is not the same
        {
            public string Text { get; set; }
            public object Value { get; set; }

            public ComboboxItem(string t,string v)
            {
                Text = t;
                Value = v;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private DataSet ds = new DataSet();
        private DataTable dt = new DataTable();

        private void ListBtn_Click(object sender, RoutedEventArgs e)
        {
            loadingBar.Visibility = Visibility.Visible;

            try
            {
                // connstring stored in App.config
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                var conn = new NpgsqlConnection(connstring);
                conn.Open();
                string sql = getSQLcommand();
                // data adapter making request from our connection
                var da = new NpgsqlDataAdapter(sql, conn);
                ds.Reset();
                // filling DataSet with result from NpgsqlDataAdapter
                da.Fill(ds);
                // since it C# DataSet can handle multiple tables, we will select first
                dt = ds.Tables[0];
                // connect grid to DataTable
                resultDataGrid.ItemsSource = dt.DefaultView;
                // since we only showing the result we don't need connection anymore
                conn.Close();

                setHeaderTexts();
                resultRowCount.Content = resultDataGrid.Items.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            //MessageBox.Show("progressBar"); // progress bar test
            
            loadingBar.Visibility = Visibility.Hidden;
        }

        private void setHeaderTexts()
        {
            
            if (prodCbx.Text.ToString() == "Lakk")
            {
                resultDataGrid.Columns[0].Header = "ID";
                resultDataGrid.Columns[1].Header = "Batch";
                resultDataGrid.Columns[2].Header = "Lejárat";
                resultDataGrid.Columns[3].Header = "Beviteli dátum";
                resultDataGrid.Columns[4].Header = "Munkaállomás";
            }
            else
            {
                resultDataGrid.Columns[0].Header = "ID";
                resultDataGrid.Columns[1].Header = "Cikkszám";
                resultDataGrid.Columns[2].Header = "Keret";
                resultDataGrid.Columns[3].Header = "Beviteli dátum";
                resultDataGrid.Columns[4].Header = "Munkaállomás";
            }
        }

        private string getSQLcommand()
        {

            string start = "'" + startDate.SelectedDate.Value.Year.ToString() + "-" + startDate.SelectedDate.Value.Month.ToString() + "-" + startDate.SelectedDate.Value.Day.ToString() + "'";
            string end = "'" + endDate.SelectedDate.Value.Year.ToString() + "-" + endDate.SelectedDate.Value.Month.ToString() + "-" + endDate.SelectedDate.Value.Day.ToString() + "'";

            string Querycmd = "SELECT * FROM " + (prodCbx.SelectedItem as ComboboxItem).Value.ToString() + " WHERE (workstation = " + (workStationCbx.SelectedItem as ComboboxItem).Value.ToString() + ") AND date(timestamp) >= " + start + " and date(timestamp) <= " + end;

            if (prodDmTbx.Text.Length > 0)
            {
                Querycmd = Querycmd + " AND prod_dm = '" + prodDmTbx.Text + "'";
            }


            return Querycmd;
        }

        private void prodCbx_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if ((prodCbx.SelectedItem as ComboboxItem).Value.ToString() == "lakk")
            {
                prodDmTbx.IsEnabled = false;
                prodDmTbx.Text = "";
            }
            else
            {
                prodDmTbx.IsEnabled = true;
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            startDate.SelectedDate = new DateTime(2017, 10, 01);
            endDate.SelectedDate = DateTime.Today;
            prodCbx.SelectedIndex = 0;
            workStationCbx.SelectedIndex = 0;
            prodDmTbx.Text = "";
            resultDataGrid.ItemsSource = null;
            resultRowCount.Content = "0";
        }

    }
}
