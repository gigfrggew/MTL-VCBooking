<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="TMG_ITC.Dashboard" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Service Selection - VC Booking</title>
    <link href="Content/bootstrap.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link href="Content/Site.css?v=20260427-clean3" rel="stylesheet" />
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <div class="rail-viewport d-flex justify-content-center align-items-center">
            <div class="rail-card premium-card text-center" style="max-width: 1250px; width: 100%; min-height: 560px;">
                
                <h1 class="page-title mb-2">Welcome, <asp:Literal ID="litUserName" runat="server"></asp:Literal></h1>
                <p class="page-subtitle mb-5">Please select a service module to continue</p>

                <div class="row g-4 justify-content-center">
                    <!-- VC Booking Module -->
                    <div class="col-md-5">
                        <asp:LinkButton ID="lnkVCBooking" runat="server" OnClick="lnkVCBooking_Click" CssClass="service-card text-start h-100">
                            <div class="service-icon-box" style="background: #eff6ff; color: #3b82f6;">
                                <i class="bi bi-camera-video"></i>
                            </div>
                            <div>
                                <h4 class="fw-bold mb-1">VC Booking</h4>
                                <p class="text-muted small mb-0">Schedule and manage your video conference meetings with ease.</p>
                            </div>
                            <i class="bi bi-arrow-right ms-auto opacity-25"></i>
                        </asp:LinkButton>
                    </div>

                    <!-- Complaint Form Module -->
                    <div class="col-md-5">
                        <asp:LinkButton ID="lnkComplaints" runat="server" OnClick="lnkComplaints_Click" CssClass="service-card text-start h-100">
                            <div class="service-icon-box" style="background: #f0f9ff; color: #0ea5e9;">
                                <i class="bi bi-file-earmark-plus"></i>
                            </div>
                            <div>
                                <h4 class="fw-bold mb-1">Complaint Form</h4>
                                <p class="text-muted small mb-0">Submit and track your technical issues or service complaints.</p>
                            </div>
                            <i class="bi bi-arrow-right ms-auto opacity-25"></i>
                        </asp:LinkButton>
                    </div>
                </div>

                <div class="mt-5 pt-4 text-center">
                    <asp:LinkButton ID="btnLogout" runat="server" OnClick="btnLogout_Click" CssClass="nav-back-link justify-content-center">
                        <i class="bi bi-box-arrow-right"></i> Sign Out
                    </asp:LinkButton>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
