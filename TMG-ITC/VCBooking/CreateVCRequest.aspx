<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="CreateVCRequest.aspx.cs" Inherits="VCBooking.CreateVCRequest" Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Create VC Request</title>

    <!-- Bootstrap CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600&display=swap" rel="stylesheet" />
    <style>
        body { font-family: 'Inter', sans-serif; }
        #processingOverlay {
            display: none;
            position: fixed;
            inset: 0;
            background: rgba(255,255,255,0.85);
            z-index: 9999;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            backdrop-filter: blur(4px);
        }
        #processingOverlay.show { display: flex; }
        .processing-card {
            background: white;
            border-radius: 16px;
            box-shadow: 0 8px 32px rgba(0,0,0,0.15);
            padding: 40px 50px;
            text-align: center;
        }
        .spinner-lg { width: 3.5rem; height: 3.5rem; }
    </style>
</head>
<body class="bg-light">

    <form id="form1" runat="server">
        <asp:ScriptManager runat="server" />
        <asp:UpdatePanel runat="server" ID="UpdatePanel1">
            <ContentTemplate>

        <div class="container mt-5 mb-5">

            <div class="card shadow-lg p-4">

                <h3 class="text-center text-primary mb-4">Create VC Request</h3>

                <!-- ================= BASIC INFO ================= -->
                <h5 class="text-secondary mb-3">Basic Information</h5>

                <div class="row mb-3">
                    <div class="col-md-6">
                        <asp:Label runat="server" ID="lblCompany" Text="Company" CssClass="form-label" />
                        <asp:DropDownList runat="server" ID="ddlCompany" CssClass="form-select" />
                    </div>

                    <div class="col-md-6">
                        <asp:Label runat="server" ID="lblTopic" Text="Topic" CssClass="form-label" />
                        <asp:TextBox runat="server" ID="txtTopic" CssClass="form-control" />
                    </div>
                </div>

                <div class="row mb-3">
                    <div class="col-md-4">
                        <asp:Label runat="server" ID="lblDate" Text="Booking Date" CssClass="form-label" />
                        <asp:TextBox ID="txtDate" runat="server" TextMode="Date"
                            AutoPostBack="true" OnTextChanged="DateOrTimeChanged"
                            CssClass="form-control" />
                    </div>

                    <div class="col-md-4">
                        <asp:Label runat="server" ID="lblFrom" Text="From Time" CssClass="form-label" />

                        <div class="d-flex gap-2">

                            <!-- Hours -->
                            <asp:DropDownList ID="ddlFromHour" runat="server"
                                AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                CssClass="form-select">
                            </asp:DropDownList>

                            <!-- Minutes -->
                            <asp:DropDownList ID="ddlFromMinute" runat="server"
                                AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                CssClass="form-select">
                                <asp:ListItem Text="Min" Value="" />
                                <asp:ListItem Text="00" Value="0" />
                                <asp:ListItem Text="15" Value="15" />
                                <asp:ListItem Text="30" Value="30" />
                                <asp:ListItem Text="45" Value="45" />
                            </asp:DropDownList>

                        </div>
                    </div>

                    <div class="col-md-4">
                        <asp:Label runat="server" ID="lblDuration" Text="Duration" CssClass="form-label" />

                        <div class="d-flex gap-2">

                            <!-- Hours -->
                            <asp:DropDownList ID="ddlHours" runat="server"
                                AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                CssClass="form-select">
                                <asp:ListItem Text="Hr" Value="" />
                                <asp:ListItem Text="0" Value="0" />
                                <asp:ListItem Text="1" Value="1" />
                                <asp:ListItem Text="2" Value="2" />
                                <asp:ListItem Text="3" Value="3" />
                                <asp:ListItem Text="4" Value="4" />
                                <asp:ListItem Text="5" Value="5" />
                                <asp:ListItem Text="6" Value="6" />
                                <asp:ListItem Text="7" Value="7" />
                                <asp:ListItem Text="8" Value="8" />
                                <asp:ListItem Text="9" Value="9" />
                                <asp:ListItem Text="10" Value="10" />
                            </asp:DropDownList>

                            <!-- Minutes -->
                            <asp:DropDownList ID="ddlMinutes" runat="server"
                                AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                CssClass="form-select">
                                <asp:ListItem Text="Min" Value="" />
                                <asp:ListItem Text="00" Value="0" />
                                <asp:ListItem Text="15" Value="15" />
                                <asp:ListItem Text="30" Value="30" />
                                <asp:ListItem Text="45" Value="45" />
                            </asp:DropDownList>

                        </div>
                    </div>
                </div>

                <!-- ================= VC DETAILS ================= -->
                <h5 class="text-secondary mt-4 mb-3">VC Details</h5>

                <div class="row mb-3">

                    <!-- LEFT COLUMN: VC Type, Account, Location stacked -->
                    <div class="col-md-4">

                        <div class="mb-2">
                            <asp:Label runat="server" ID="lblVCType" Text="VC Type" CssClass="form-label" />
                            <asp:DropDownList runat="server" ID="ddlVCType"
                                AutoPostBack="true"
                                OnSelectedIndexChanged="ddlVCType_SelectedIndexChanged"
                                CssClass="form-select form-select-sm">
                            </asp:DropDownList>
                        </div>

                        <div class="mb-2">
                            <asp:Label runat="server" ID="lblVCAccount" Text="VC Account" CssClass="form-label" />
                            <asp:DropDownList runat="server" ID="ddlVCAccount" CssClass="form-select form-select-sm">
                            </asp:DropDownList>
                        </div>

                        <div class="mb-2">
                            <asp:Label runat="server" ID="lblLocation" Text="Location" CssClass="form-label" />
                            <asp:DropDownList runat="server" ID="ddlLocation" CssClass="form-select form-select-sm">
                            </asp:DropDownList>
                        </div>

                    </div>

                    <!-- RIGHT COLUMN: Booked Meetings Preview -->
                    <div class="col-md-8">
                        <label class="form-label fw-semibold text-secondary">
                            <i class="bi bi-calendar2-check"></i> Booked Meetings on Selected Slot
                        </label>
                        <div class="border rounded p-2 bg-white" style="min-height: 160px; max-height: 270px; overflow-y: auto;">
                            <asp:GridView runat="server"
                                ID="gvBookedSlots"
                                CssClass="table table-sm table-bordered mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No meetings booked on this slot."
                                GridLines="None">
                                <Columns>
                                    <asp:BoundField DataField="VCAccountName" HeaderText="Account" />
                                    <asp:BoundField DataField="Topic"         HeaderText="Topic" />
                                    <asp:BoundField DataField="FromTime"      HeaderText="From"  DataFormatString="{0:HH:mm}" />
                                    <asp:BoundField DataField="ToTime"        HeaderText="To"    DataFormatString="{0:HH:mm}" />
                                    <asp:BoundField DataField="VCStatus"      HeaderText="Status" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>

                </div>

                <!-- Unit / Floor  +  VC Details — unchanged, full width -->
                <div class="row mb-3">
                    <div class="col-md-6">
                        <asp:Label runat="server" ID="lblUnitFloor" Text="Unit / Floor Details" CssClass="form-label" />
                        <asp:TextBox runat="server" ID="txtUnitFloor" CssClass="form-control" />
                    </div>

                    <div class="col-md-6">
                        <asp:Label runat="server" ID="lblVCDetails" Text="VC Details" CssClass="form-label" />
                        <asp:TextBox runat="server" ID="txtVCDetails" TextMode="MultiLine" Rows="3" CssClass="form-control" />
                    </div>
                </div>

                <!-- ================= PARTICIPANTS ================= -->
                <h5 class="text-secondary mt-4 mb-3">Participants</h5>

                <div class="row mb-3">
                    <div class="col-md-8">
                        <asp:TextBox runat="server" ID="txtParticipant"
                            CssClass="form-control"
                            placeholder="Enter email OR multiple emails separated by comma" />
                    </div>

                    <div class="col-md-4">
                        <asp:Button runat="server"
                            ID="btnParticipant"
                            Text="Add Participant"
                            OnClick="btnAddParticipant_Click"
                            CssClass="btn btn-success w-100" />
                    </div>
                </div>

                <asp:Label runat="server" ID="lblParticipantMessage" CssClass="text-danger mb-3 d-block" />

                <asp:GridView runat="server"
                    ID="gvParticipants"
                    CssClass="table table-bordered table-striped"
                    AutoGenerateColumns="true">
                </asp:GridView>

                <div class="text-center mt-4">
                    <asp:Button runat="server"
                        ID="btnFormSubmit"
                        Text="Create VC Request"
                        OnClick="btnFormSubmit_Click"
                        UseSubmitBehavior="true"
                        OnClientClick="showProcessingOverlay(); return true;"
                        CssClass="btn btn-primary btn-lg px-5" />
                </div>

            </div>
        </div>

            </ContentTemplate>
            <Triggers>
                <asp:PostBackTrigger ControlID="btnFormSubmit" />
            </Triggers>
        </asp:UpdatePanel>
    </form>
    <!-- Processing Overlay -->
    <div id="processingOverlay">
        <div class="processing-card">
            <div class="spinner-border text-primary spinner-lg mb-3" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <h5 class="fw-bold mb-1">Processing Your Request</h5>
            <p class="text-secondary mb-0">Creating your Zoom meeting, please wait...</p>
        </div>
    </div>

    <!-- Bootstrap JS -->
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        function showProcessingOverlay() {
            document.getElementById('processingOverlay').classList.add('show');
        }
    </script>
</body>
</html>