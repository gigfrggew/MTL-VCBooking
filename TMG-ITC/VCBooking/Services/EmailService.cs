using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VCBooking.Services
{
    /// <summary>
    /// Service for sending HTML email notifications for meeting lifecycle events.
    /// All send methods are async – failures are caught and logged internally.
    /// </summary>
    public class EmailService
    {
        private readonly string _fromEmail;
        private readonly string _smtpPassword;
        private readonly string _fromName;

        public EmailService(string vcAccountId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(@"
            SELECT VC_Email, VC_Email_Password
            FROM VC_Account_Master
            WHERE VCAccountId = @VCAccountId", conn);

                cmd.Parameters.AddWithValue("@VCAccountId", vcAccountId);

                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    _fromEmail = reader["VC_Email"].ToString();
                    _smtpPassword = reader["VC_Email_Password"].ToString();
                }
                else
                {
                    throw new Exception("Email credentials not found in DB");
                }
            }

            _fromName = "VC System";
        }



        // ─── Booking Created ──────────────────────────────────────────────────────

        /// <summary>
        /// Sends a meeting invitation email to all participants.
        /// </summary>
        public async Task SendMeetingInviteAsync(
            string topic, DateTime startDateTime, int durationMinutes,
            string joinUrl, string password, List<string> recipientEmails, string meetingId = null, int sequence = 0)
        {
            if (recipientEmails == null || recipientEmails.Count == 0) return;

            string subject = "Invitation: " + topic;
            string body = BuildInviteBody(topic, startDateTime, durationMinutes, joinUrl, meetingId, password);

            byte[] icsBytes = BuildIcsContent(topic, startDateTime, durationMinutes, joinUrl, meetingId, "REQUEST", sequence);
            await SendToAllAsync(subject, body, recipientEmails, icsBytes);
        }

        // ─── Rescheduled ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a reschedule notification to all participants.
        /// </summary>
        public async Task SendRescheduleNotificationAsync(
            string topic,
            DateTime oldDate, TimeSpan oldFromTime, TimeSpan oldToTime,
            DateTime newDate, TimeSpan newFromTime, TimeSpan newToTime,
            string joinUrl, string meetingId, string password,
            string reason,
            List<string> recipientEmails,
            int sequence = 1)
        {
            if (recipientEmails == null || recipientEmails.Count == 0) return;

            string subject = "Meeting Rescheduled: " + topic;
            string body = BuildRescheduleBody(topic, oldDate, oldFromTime, oldToTime,
                                              newDate, newFromTime, newToTime,
                                              joinUrl, meetingId, password, reason);

            DateTime startDateTime = newDate.Date.Add(newFromTime);
            int durationMinutes = (int)(newToTime - newFromTime).TotalMinutes;
            byte[] icsBytes = BuildIcsContent(topic, startDateTime, durationMinutes, joinUrl, meetingId, "REQUEST", sequence);

            await SendToAllAsync(subject, body, recipientEmails, icsBytes);
        }

        // ─── Cancelled ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a cancellation notification to all participants.
        /// </summary>
        public async Task SendCancellationNotificationAsync(
            string topic, DateTime meetingDate, TimeSpan fromTime, TimeSpan toTime,
            string meetingId, string reason,
            List<string> recipientEmails,
            string cancelledBy,
            bool isAdmin,
            int sequence = 2)
        {
            if (recipientEmails == null || recipientEmails.Count == 0) return;

            string subject = "Meeting Cancelled: " + topic;
            string body = BuildCancellationBody(topic, meetingDate, fromTime, toTime, meetingId, reason, cancelledBy, isAdmin);

            DateTime startDateTime = meetingDate.Date.Add(fromTime);
            int durationMinutes = (int)(toTime - fromTime).TotalMinutes;
            byte[] icsBytes = BuildIcsContent(topic, startDateTime, durationMinutes, null, meetingId, "CANCEL", sequence);

            await SendToAllAsync(subject, body, recipientEmails, icsBytes);
        }

        // ─── Expired ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends an expiry notification.
        /// </summary>
        public async Task SendExpiryNotificationAsync(
            string topic, DateTime meetingDate, TimeSpan fromTime, TimeSpan toTime,
            string meetingId, List<string> recipientEmails)
        {
            if (recipientEmails == null || recipientEmails.Count == 0) return;

            string subject = "Meeting Expired: " + topic;
            string body = BuildExpiryBody(topic, meetingDate, fromTime, toTime, meetingId);
            await SendToAllAsync(subject, body, recipientEmails);
        }

        // ─── Internal helpers ─────────────────────────────────────────────────────

        private async Task SendToAllAsync(string subject, string body, List<string> emails, byte[] attachmentBytes = null)
        {
            try
            {
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.Credentials = new NetworkCredential(_fromEmail, _smtpPassword);
                    client.EnableSsl = true;

                    foreach (var email in emails)
                    {
                        if (string.IsNullOrWhiteSpace(email)) continue;

                        try
                        {
                            using (var mail = new MailMessage())
                            {
                                mail.From = new MailAddress(_fromEmail, _fromName);
                                mail.To.Add(email.Trim());
                                mail.Subject = subject;
                                mail.Body = body;
                                mail.IsBodyHtml = true;

                                if (attachmentBytes != null)
                                {
                                    using (var ms = new MemoryStream(attachmentBytes))
                                    {
                                        Attachment attachment = new Attachment(ms, "invite.ics", "text/calendar");
                                        mail.Attachments.Add(attachment);
                                        await client.SendMailAsync(mail);
                                    }
                                }
                                else
                                {
                                    await client.SendMailAsync(mail);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("[EmailService] Failed to send to {0}: {1}", email, ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[EmailService] Error sending '{0}': {1}", subject, ex.Message));
            }
        }

        // ─── HTML body builders ───────────────────────────────────────────────────

        private string BuildInviteBody(string topic, DateTime startDateTime, int durationMinutes,
            string joinUrl, string meetingId, string password)
        {
            var sb = new StringBuilder();
            OpenCard(sb, "#0d6efd", "Video Conference Invitation",
                "You have been invited to a video conference meeting.");
            sb.AppendLine("<table style='width:100%;border-collapse:collapse;'>");
            Row(sb, "Topic", topic);
            Row(sb, "Date", startDateTime.ToString("dddd, MMMM dd, yyyy"));
            Row(sb, "Time", string.Format("{0} – {1}",
                startDateTime.ToString("hh:mm tt"),
                startDateTime.AddMinutes(durationMinutes).ToString("hh:mm tt")));
            sb.AppendLine("</table>");
            JoinButton(sb, joinUrl);
            MeetingInfo(sb, meetingId, password);
            CloseCard(sb);
            return sb.ToString();
        }

        private string BuildRescheduleBody(
            string topic,
            DateTime oldDate, TimeSpan oldFrom, TimeSpan oldTo,
            DateTime newDate, TimeSpan newFrom, TimeSpan newTo,
            string joinUrl, string meetingId, string password, string reason)
        {
            var sb = new StringBuilder();
            OpenCard(sb, "#fd7e14", "Meeting Rescheduled",
                "The following meeting has been rescheduled by the administrator.");
            sb.AppendLine("<table style='width:100%;border-collapse:collapse;'>");
            Row(sb, "Topic", topic);
            Row(sb, "Previous Schedule",
                string.Format("{0} &nbsp; {1:hh\\:mm tt} – {2:hh\\:mm tt}",
                    oldDate.ToString("dddd, MMMM dd, yyyy"),
                    DateTime.Today.Add(oldFrom), DateTime.Today.Add(oldTo)));
            Row(sb, "<strong>New Schedule</strong>",
                string.Format("<strong>{0} &nbsp; {1:hh\\:mm tt} – {2:hh\\:mm tt}</strong>",
                    newDate.ToString("dddd, MMMM dd, yyyy"),
                    DateTime.Today.Add(newFrom), DateTime.Today.Add(newTo)));
            if (!string.IsNullOrWhiteSpace(reason))
                Row(sb, "Reason", reason);
            sb.AppendLine("</table>");
            JoinButton(sb, joinUrl);
            MeetingInfo(sb, meetingId, password);
            CloseCard(sb);
            return sb.ToString();
        }

        private string BuildCancellationBody(
    string topic, DateTime meetingDate, TimeSpan fromTime, TimeSpan toTime,
    string meetingId, string reason, string cancelledBy,
    bool isAdmin)
        {
            var sb = new StringBuilder();

            // 🔥 Dynamic text logic
            string cancelledText;
            if (isAdmin)
            {
                cancelledText = "the administrator";
            }
            else
            {
                cancelledText = cancelledBy;
            }

            // ✅ Use dynamic text in UI
            OpenCard(sb, "#dc3545", "Meeting Cancelled",
                $"The following meeting has been cancelled by <b>{cancelledText}</b>.");

            sb.AppendLine("<table style='width:100%;border-collapse:collapse;'>");

            Row(sb, "Topic", topic);
            Row(sb, "Date", meetingDate.ToString("dddd, MMMM dd, yyyy"));
            Row(sb, "Time", string.Format("{0:hh\\:mm tt} – {1:hh\\:mm tt}",
                DateTime.Today.Add(fromTime), DateTime.Today.Add(toTime)));

            if (!string.IsNullOrWhiteSpace(meetingId))
                Row(sb, "Meeting ID", meetingId);

            if (!string.IsNullOrWhiteSpace(reason))
                Row(sb, "Reason", reason);

            sb.AppendLine("</table>");

            CloseCard(sb);

            return sb.ToString();
        }

        private string BuildExpiryBody(
            string topic, DateTime meetingDate, TimeSpan fromTime, TimeSpan toTime,
            string meetingId)
        {
            var sb = new StringBuilder();
            OpenCard(sb, "#6c757d", "Meeting Expired",
                "The following meeting was automatically marked as expired because it was not started within the grace period.");
            sb.AppendLine("<table style='width:100%;border-collapse:collapse;'>");
            Row(sb, "Topic", topic);
            Row(sb, "Date", meetingDate.ToString("dddd, MMMM dd, yyyy"));
            Row(sb, "Time", string.Format("{0:hh\\:mm tt} – {1:hh\\:mm tt}",
                DateTime.Today.Add(fromTime), DateTime.Today.Add(toTime)));
            if (!string.IsNullOrWhiteSpace(meetingId))
                Row(sb, "Meeting ID", meetingId);
            sb.AppendLine("</table>");
            CloseCard(sb);
            return sb.ToString();
        }

        // ─── Card layout helpers ──────────────────────────────────────────────────

        private void OpenCard(StringBuilder sb, string accentColor, string heading, string intro)
        {
            sb.AppendFormat(@"
<div style='font-family:Arial,sans-serif;max-width:620px;margin:0 auto;padding:24px;
            border:1px solid #ddd;border-radius:10px;background:#fff;'>
  <div style='border-left:5px solid {0};padding-left:14px;margin-bottom:20px;'>
    <h2 style='color:{0};margin:0 0 4px;'>{1}</h2>
    <p style='margin:0;color:#555;'>{2}</p>
  </div>
  <hr style='border:0;border-top:1px solid #eee;margin-bottom:18px;' />", accentColor, heading, intro);
        }

        private void CloseCard(StringBuilder sb)
        {
            sb.AppendLine(@"
  <p style='font-size:0.8em;color:#aaa;margin-top:30px;text-align:center;'>
    This is an automated message from the ITC VC Booking System. Please do not reply.
  </p>
</div>");
        }

        private void Row(StringBuilder sb, string label, string value)
        {
            sb.AppendFormat(@"
<tr>
  <td style='padding:7px 0;font-weight:bold;width:160px;color:#333;vertical-align:top;'>{0}:</td>
  <td style='padding:7px 0;color:#222;'>{1}</td>
</tr>", label, value);
        }

        private void JoinButton(StringBuilder sb, string joinUrl)
        {
            if (string.IsNullOrWhiteSpace(joinUrl)) return;
            sb.AppendFormat(@"
<div style='margin:25px 0;text-align:center;'>
  <a href='{0}' style='background:#0d6efd;color:#fff;padding:13px 28px;text-decoration:none;
     border-radius:25px;font-weight:bold;display:inline-block;font-size:1em;'>
    &#128248;&nbsp; Join Zoom Meeting
  </a>
</div>", joinUrl);
        }

        private void MeetingInfo(StringBuilder sb, string meetingId, string password)
        {
            if (string.IsNullOrWhiteSpace(meetingId) && string.IsNullOrWhiteSpace(password)) return;
            sb.AppendLine("<div style='background:#f8f9fa;padding:14px;border-radius:6px;font-size:0.9em;color:#555;margin-top:10px;'>");
            if (!string.IsNullOrWhiteSpace(meetingId))
                sb.AppendFormat("<strong>Meeting ID:</strong> {0}<br/>", meetingId);
            if (!string.IsNullOrWhiteSpace(password))
                sb.AppendFormat("<strong>Passcode:</strong> {0}", password);
            sb.AppendLine("</div>");
        }
        // ─── ICS Helper ───────────────────────────────────────────────────────────

        private byte[] BuildIcsContent(string topic, DateTime startDateTime, int durationMinutes,
            string joinUrl, string meetingId, string method, int sequence)
        {
            var sb = new StringBuilder();
            string dtStamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            string dtStart = startDateTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            string dtEnd = startDateTime.AddMinutes(durationMinutes).ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            string uid = string.Format("{0}@{1}", meetingId ?? Guid.NewGuid().ToString(), "itc.vc.system");

            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//ITC VC Booking//VC System//EN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:" + method);
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine("UID:" + uid);
            sb.AppendLine("DTSTAMP:" + dtStamp);
            sb.AppendLine("DTSTART:" + dtStart);
            sb.AppendLine("DTEND:" + dtEnd);
            sb.AppendLine("SUMMARY:" + topic);
            sb.AppendLine("DESCRIPTION:Video Conference Meeting.\\nJoin Zoom Meeting: " + (joinUrl ?? "N/A"));
            sb.AppendLine("LOCATION:Zoom Meeting");
            sb.AppendLine("ORGANIZER;CN=VC System:mailto:" + _fromEmail);
            sb.AppendLine("SEQUENCE:" + sequence);
            sb.AppendLine("STATUS:" + (method == "CANCEL" ? "CANCELLED" : "CONFIRMED"));
            sb.AppendLine("CLASS:PUBLIC");

            // Add standard 15-minute reminder
            sb.AppendLine("BEGIN:VALARM");
            sb.AppendLine("TRIGGER:-PT15M");
            sb.AppendLine("ACTION:DISPLAY");
            sb.AppendLine("DESCRIPTION:Reminder: " + topic);
            sb.AppendLine("END:VALARM");

            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}