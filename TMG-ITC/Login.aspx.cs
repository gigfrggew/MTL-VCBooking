using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;

namespace TMG_ITC
{
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string empCode = txtEmployeecode.Text.Trim();

            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection con = new SqlConnection(connStr))
            {
                string query = "SELECT EMP_Name, EMP_EmailId FROM TBL_EmployeeDetails WHERE EMP_Empcode = @EmpCode AND EMP_DomainUsername = @EmpCode";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@EmpCode", empCode);

                con.Open();
                bool isAuthenticated = false;
                string userName = "";
                string userEmail = "";

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        isAuthenticated = true;
                        userName = reader["EMP_Name"].ToString();
                        userEmail = reader["EMP_EmailId"].ToString();
                    }
                } // First reader closes here

                if (isAuthenticated)
                {
                    Session["EmployeeCode"] = empCode;
                    Session["UserName"] = userName;
                    Session["UserEmail"] = userEmail;
                    
                    // Check if Admin requires the connection to be free of open readers
                    bool isAdmin = false;
                    string adminQuery = "SELECT 1 FROM TBL_VC_Admins WHERE EmpCode = @EmpCode";
                    using (SqlCommand adminCmd = new SqlCommand(adminQuery, con))
                    {
                        adminCmd.Parameters.AddWithValue("@EmpCode", empCode);
                        using (SqlDataReader adminReader = adminCmd.ExecuteReader())
                        {
                            if (adminReader.Read())
                            {
                                isAdmin = true;
                            }
                        }
                    }
                    Session["IsAdmin"] = isAdmin;

                    ScriptManager.RegisterStartupScript(this, GetType(), "LoginSuccess", "showLoginResult(true);", true);
                }
                else
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "LoginFail", "showLoginResult(false, 'Invalid Credentials');", true);
                }
            }
        }
    }
}