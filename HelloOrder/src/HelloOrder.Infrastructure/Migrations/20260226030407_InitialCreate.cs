using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelloOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commission_record",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<string>(type: "text", nullable: false),
                    PeriodValue = table.Column<string>(type: "text", nullable: false),
                    SalesAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commission_record", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "commission_rule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleType = table.Column<string>(type: "text", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    Config = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commission_rule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversion_job",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    quotation_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tracking_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    extracted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    merchant_folder = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Remark = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversion_job", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "express_company",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Contact = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_express_company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseNo = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_operation_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Module = table.Column<string>(type: "text", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    Ip = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_operation_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_permission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DataScope = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_receipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNo = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_receipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversion_quotation_sheet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sheet_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sheet_type = table.Column<int>(type: "integer", nullable: false),
                    column_names_json = table.Column<string>(type: "text", nullable: true),
                    content_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversion_quotation_sheet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversion_quotation_sheet_conversion_job_conversion_job_id",
                        column: x => x.conversion_job_id,
                        principalTable: "conversion_job",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversion_tracking_sheet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sheet_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    column_names_json = table.Column<string>(type: "text", nullable: true),
                    content_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversion_tracking_sheet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversion_tracking_sheet_conversion_job_conversion_job_id",
                        column: x => x.conversion_job_id,
                        principalTable: "conversion_job",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "express_country_rate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    express_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    country_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: true),
                    lead_days_min = table.Column<int>(type: "integer", nullable: true),
                    lead_days_max = table.Column<int>(type: "integer", nullable: true),
                    charge_rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_express_country_rate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_express_country_rate_express_company_express_company_id",
                        column: x => x.express_company_id,
                        principalTable: "express_company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    ProductName = table.Column<string>(type: "text", nullable: true),
                    Spec = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Link1688 = table.Column<string>(type: "text", nullable: true),
                    Supplier = table.Column<string>(type: "text", nullable: true),
                    ExpressNo = table.Column<string>(type: "text", nullable: true),
                    ReceivedQty = table.Column<int>(type: "integer", nullable: false),
                    OrderItemIds = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_item_purchase_order_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_role_permission",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_role_permission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_sys_role_permission_sys_permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "sys_permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sys_role_permission_sys_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "sys_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    RealName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_user_sys_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "sys_role",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "merchant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ShopName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Contact = table.Column<string>(type: "text", nullable: true),
                    SettlementType = table.Column<string>(type: "text", nullable: true),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_sys_user_BusinessUserId",
                        column: x => x.BusinessUserId,
                        principalTable: "sys_user",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_business_assistant",
                columns: table => new
                {
                    business_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assistant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_business_assistant", x => new { x.business_user_id, x.assistant_user_id });
                    table.ForeignKey(
                        name: "FK_user_business_assistant_sys_user_assistant_user_id",
                        column: x => x.assistant_user_id,
                        principalTable: "sys_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_business_assistant_sys_user_business_user_id",
                        column: x => x.business_user_id,
                        principalTable: "sys_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_shop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ShopUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_shop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merchant_shop_merchant_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationNo = table.Column<string>(type: "text", nullable: false),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotation_merchant_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNo = table.Column<string>(type: "text", nullable: false),
                    PlatformOrderId = table.Column<string>(type: "text", nullable: true),
                    BusinessUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssistantUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    OrderTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_merchant_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "merchant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_merchant_shop_merchant_shop_id",
                        column: x => x.merchant_shop_id,
                        principalTable: "merchant_shop",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_en = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Spec = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PlatformUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    weight_g = table.Column<decimal>(type: "numeric", nullable: true),
                    length_cm = table.Column<decimal>(type: "numeric", nullable: true),
                    width_cm = table.Column<decimal>(type: "numeric", nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric", nullable: true),
                    Link1688 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    customer_link = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    factory_link = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    material = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    style_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    size_chart = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    package_note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    suggested_purchase_price = table.Column<decimal>(type: "numeric", nullable: true),
                    suggested_sale_price = table.Column<decimal>(type: "numeric", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_merchant_shop_merchant_shop_id",
                        column: x => x.merchant_shop_id,
                        principalTable: "merchant_shop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotation_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    ProductName = table.Column<string>(type: "text", nullable: true),
                    Spec = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Link1688 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotation_item_quotation_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "quotation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNo = table.Column<string>(type: "text", nullable: true),
                    ExpressCompany = table.Column<string>(type: "text", nullable: true),
                    ExpressNo = table.Column<string>(type: "text", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric", nullable: true),
                    PackedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    ProductName = table.Column<string>(type: "text", nullable: true),
                    Spec = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Link1688 = table.Column<string>(type: "text", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_item_order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_item_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "shipment_item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_item_shipment_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversion_quotation_sheet_conversion_job_id",
                table: "conversion_quotation_sheet",
                column: "conversion_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversion_tracking_sheet_conversion_job_id",
                table: "conversion_tracking_sheet",
                column: "conversion_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_express_country_rate_express_company_id",
                table: "express_country_rate",
                column: "express_company_id");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_BusinessUserId",
                table: "merchant",
                column: "BusinessUserId");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_shop_merchant_id",
                table: "merchant_shop",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_merchant_shop_id",
                table: "order",
                column: "merchant_shop_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_MerchantId",
                table: "order",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_order_OrderNo",
                table: "order",
                column: "OrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_OrderId",
                table: "order_item",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_product_id",
                table: "order_item",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_merchant_shop_id",
                table: "product",
                column: "merchant_shop_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_item_PurchaseOrderId",
                table: "purchase_item",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_PurchaseNo",
                table: "purchase_order",
                column: "PurchaseNo");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_MerchantId",
                table: "quotation",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_QuotationNo",
                table: "quotation",
                column: "QuotationNo");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_item_QuotationId",
                table: "quotation_item",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_OrderId",
                table: "shipment",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_item_ShipmentId",
                table: "shipment_item",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_role_permission_PermissionId",
                table: "sys_role_permission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_RoleId",
                table: "sys_user",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_Username",
                table: "sys_user",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_business_assistant_assistant_user_id",
                table: "user_business_assistant",
                column: "assistant_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_record");

            migrationBuilder.DropTable(
                name: "commission_rule");

            migrationBuilder.DropTable(
                name: "conversion_quotation_sheet");

            migrationBuilder.DropTable(
                name: "conversion_tracking_sheet");

            migrationBuilder.DropTable(
                name: "express_country_rate");

            migrationBuilder.DropTable(
                name: "order_item");

            migrationBuilder.DropTable(
                name: "purchase_item");

            migrationBuilder.DropTable(
                name: "quotation_item");

            migrationBuilder.DropTable(
                name: "shipment_item");

            migrationBuilder.DropTable(
                name: "sys_operation_log");

            migrationBuilder.DropTable(
                name: "sys_role_permission");

            migrationBuilder.DropTable(
                name: "user_business_assistant");

            migrationBuilder.DropTable(
                name: "warehouse_receipt");

            migrationBuilder.DropTable(
                name: "conversion_job");

            migrationBuilder.DropTable(
                name: "express_company");

            migrationBuilder.DropTable(
                name: "product");

            migrationBuilder.DropTable(
                name: "purchase_order");

            migrationBuilder.DropTable(
                name: "quotation");

            migrationBuilder.DropTable(
                name: "shipment");

            migrationBuilder.DropTable(
                name: "sys_permission");

            migrationBuilder.DropTable(
                name: "order");

            migrationBuilder.DropTable(
                name: "merchant_shop");

            migrationBuilder.DropTable(
                name: "merchant");

            migrationBuilder.DropTable(
                name: "sys_user");

            migrationBuilder.DropTable(
                name: "sys_role");
        }
    }
}
