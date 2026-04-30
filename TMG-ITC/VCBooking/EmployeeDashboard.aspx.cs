using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace VCBooking
{
    public partial class Dashboard : System.Web.UI.Page
    {
        protected async void Page_Load(object sender, EventArgs e)
        {
            if (Session["EmployeeCode"] == null)
            {
                Response.Redirect("~/Login.aspx");
            }

            if (!IsPostBack)
            {
                await CleanupExpiredMeetings();

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

        private async Task CleanupExpiredMeetings()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(
                    ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
                {
                    await con.OpenAsync();

                    string query = @"
                SELECT VCId, MeetingId, VCAccountId, Platform
                FROM VCRequestHeader
                WHERE DATEADD(MINUTE, 10, ToTime) < GETDATE()
                AND VCStatus IN ('Booked', 'Rescheduled')";

                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    var expiredMeetings = new List<Tuple<string, string, string, string>>();

                    while (await reader.ReadAsync())
                    {
                        expiredMeetings.Add(new Tuple<string, string, string, string>(
                            reader["VCId"].ToString(),
                            reader["MeetingId"].ToString(),
                            reader["VCAccountId"].ToString(),
                            reader["Platform"].ToString()
                        ));
                    }

                    reader.Close();

                    foreach (var meeting in expiredMeetings)
                    {
                        try
                        {
                            string vcId = meeting.Item1;
                            string meetingId = meeting.Item2;
                            string vcAccountId = meeting.Item3;
                            string platform = meeting.Item4;
                            string apiStatus = "DeleteSkipped";

                            if (!string.IsNullOrWhiteSpace(meetingId))
                            {
                                try
                                {
                                    if (platform.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var zoomService = new Services.ZoomService(vcAccountId);
                                        await zoomService.DeleteMeetingAsync(meetingId);
                                        apiStatus = "Deleted";
                                    }
                                    else if (platform.IndexOf("Google", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var googleService = new Services.GoogleMeetService(vcAccountId);
                                        await googleService.DeleteMeetingAsync(meetingId);
                                        apiStatus = "Deleted";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    apiStatus = "DeleteFailed";
                                    System.Diagnostics.Debug.WriteLine("Cleanup API delete error: " + ex.Message);
                                }
                            }

                            string updateQuery = @"
                        UPDATE VCRequestHeader
                        SET VCStatus = 'Completed',
                            APIStatus = @APIStatus
                        WHERE VCId = @VCId";

                            SqlCommand updateCmd = new SqlCommand(updateQuery, con);
                            updateCmd.Parameters.AddWithValue("@VCId", vcId);
                            updateCmd.Parameters.AddWithValue("@APIStatus", apiStatus);

                            await updateCmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Cleanup error: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CleanupExpiredMeetings error: " + ex.Message);
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
