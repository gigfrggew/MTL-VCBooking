using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace VCBooking
{
    public partial class Dashboard : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["EmployeeCode"] == null)
            {
                Response.Redirect("~/Login.aspx");
            }

            if (!IsPostBack)
            {
                lblWelcome.Text = "" + Session["username"];

                bool isAdmin = Session["IsAdmin"] != null && (bool)Session["IsAdmin"];

                if (isAdmin)
                {
                    btnAdminDashboard.Visible = true;
                }
                else
                {
                    btnAdminDashboard.Visible = false;
                    btnViewRequests.Visible = true;   // (optional but clean)
                }
            }
        }

        protected void btnClick_createVCRequest(object sender, EventArgs e)
        {
            Response.Redirect("CreateVCRequest.aspx");
        }

        protected void btnClick_viewRequests(object sender, EventArgs e)
        {
            Response.Redirect("ViewRequests.aspx");
        }

        protected void btnClick_adminDashboard(object sender, EventArgs e)
        {
            Response.Redirect("AdminDashboard.aspx");
        }

        protected void btnClick_LogOut(object sender, EventArgs e)
        {
            Session.Clear();
            Session.Abandon();
            Response.Redirect("~/Login.aspx"); 
        }
    }
}