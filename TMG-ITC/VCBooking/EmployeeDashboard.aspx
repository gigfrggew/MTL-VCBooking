<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="EmployeeDashboard.aspx.cs" Inherits="VCBooking.Dashboard" Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>VC Dashboard - VC Booking</title>
    <link href="../Content/bootstrap.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link href="../Content/Site.css?v=20260427-clean3" rel="stylesheet" />
    <style>
        .dash-item {
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
            border: 1px solid rgba(0,0,0,0.02) !important;
        }

        .dash-item:hover {
            transform: scale(1.05) !important;
            box-shadow: 0 15px 30px rgba(0,0,0,0.08) !important;
            background: #fff !important;
            border-color: var(--vc-primary) !important;
        }

        .dash-item-icon {
            transition: transform 0.3s ease;
        }

        .dash-item:hover .dash-item-icon {
            transform: scale(1.1) rotate(5deg);
        }

        .animate-up {
            animation: fadeInUp 0.6s both;
        }

        @keyframes fadeInUp {
            from { opacity: 0; transform: translateY(20px); }
            to { opacity: 1; transform: translateY(0); }
        }

        .delay-1 { animation-delay: 0.1s; }
        .delay-2 { animation-delay: 0.2s; }
        .delay-3 { animation-delay: 0.3s; }
    </style>
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <div class="rail-viewport employee-dashboard-viewport d-flex justify-content-center align-items-center">
            <div class="rail-card premium-card employee-dashboard-card">

                <div class="d-flex justify-content-between align-items-start mb-3">
                    <a href="../Dashboard.aspx" class="nav-back-link">
                        <i class="bi bi-arrow-left"></i> Switch Module
                    </a>
                </div>

                <div class="text-center">
                    <h1 class="page-title mb-2">Welcome, <asp:Label runat="server" ID="lblWelcome"></asp:Label></h1>
                    <p class="page-subtitle mb-4">What would you like to do today?</p>

                    <div class="dash-grid employee-dash-grid">
                        <!-- Create Request -->
                        <asp:LinkButton runat="server" ID="btnCreateVCRequest" OnClick="btnClick_createVCRequest" CssClass="dash-item animate-up delay-1">
                            <div class="dash-item-icon">
                                <i class="bi bi-calendar-plus"></i>
                            </div>
                            <h4 class="fw-bold">Create VC Request</h4>
                        </asp:LinkButton>

                        <!-- My Requests -->
                        <asp:LinkButton runat="server" ID="btnViewRequests" OnClick="btnClick_viewRequests" CssClass="dash-item animate-up delay-2">
                            <div class="dash-item-icon">
                                <i class="bi bi-file-earmark-text"></i>
                            </div>
                            <h4 class="fw-bold">My Requests</h4>
                        </asp:LinkButton>

                        <!-- Admin Dashboard -->
                        <asp:LinkButton runat="server" ID="btnAdminDashboard" OnClick="btnClick_adminDashboard" Visible="false" CssClass="dash-item animate-up delay-3">
                            <div class="dash-item-icon">
                                <i class="bi bi-box"></i>
                            </div>
                            <h4 class="fw-bold">Admin Dashboard</h4>
                        </asp:LinkButton>
                    </div>
                </div>

                <div class="mt-4 text-center">
                    <asp:LinkButton ID="btnLogOut" runat="server" OnClick="btnClick_LogOut" CssClass="nav-back-link justify-content-center">
                        <i class="bi bi-box-arrow-right"></i> Sign Out
                    </asp:LinkButton>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
