using HelloOrder.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SysUser> SysUsers => Set<SysUser>();
    public DbSet<SysRole> SysRoles => Set<SysRole>();
    public DbSet<SysPermission> SysPermissions => Set<SysPermission>();
    public DbSet<SysRolePermission> SysRolePermissions => Set<SysRolePermission>();
    public DbSet<SysOperationLog> SysOperationLogs => Set<SysOperationLog>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantShop> MerchantShops => Set<MerchantShop>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<WarehouseReceipt> WarehouseReceipts => Set<WarehouseReceipt>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<CommissionRecord> CommissionRecords => Set<CommissionRecord>();
    public DbSet<UserBusinessAssistant> UserBusinessAssistants => Set<UserBusinessAssistant>();
    public DbSet<ExpressCompany> ExpressCompanies => Set<ExpressCompany>();
    public DbSet<ExpressCountryRate> ExpressCountryRates => Set<ExpressCountryRate>();
    public DbSet<ConversionJob> ConversionJobs => Set<ConversionJob>();
    public DbSet<ConversionQuotationSheet> ConversionQuotationSheets => Set<ConversionQuotationSheet>();
    public DbSet<ConversionTrackingSheet> ConversionTrackingSheets => Set<ConversionTrackingSheet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SysUser>(e =>
        {
            e.ToTable("sys_user");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.RealName).HasMaxLength(64);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<SysRole>(e =>
        {
            e.ToTable("sys_role");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<SysPermission>(e =>
        {
            e.ToTable("sys_permission");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(64);
            e.Property(x => x.Path).HasMaxLength(256);
        });

        modelBuilder.Entity<SysRolePermission>(e =>
        {
            e.ToTable("sys_role_permission");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<SysOperationLog>(e =>
        {
            e.ToTable("sys_operation_log");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<UserBusinessAssistant>(e =>
        {
            e.ToTable("user_business_assistant");
            e.HasKey(x => new { x.BusinessUserId, x.AssistantUserId });
            e.Property(x => x.BusinessUserId).HasColumnName("business_user_id");
            e.Property(x => x.AssistantUserId).HasColumnName("assistant_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.BusinessUser).WithMany().HasForeignKey(x => x.BusinessUserId);
            e.HasOne(x => x.AssistantUser).WithMany().HasForeignKey(x => x.AssistantUserId);
        });

        modelBuilder.Entity<Merchant>(e =>
        {
            e.ToTable("merchant");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Platform).HasMaxLength(32);
            e.Property(x => x.ShopName).HasMaxLength(128);
        });

        modelBuilder.Entity<MerchantShop>(e =>
        {
            e.ToTable("merchant_shop");
            e.HasKey(x => x.Id);
            e.Property(x => x.MerchantId).HasColumnName("merchant_id");
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Platform).HasMaxLength(32);
            e.Property(x => x.ShopUrl).HasMaxLength(512);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("product");
            e.HasKey(x => x.Id);
            e.Property(x => x.MerchantShopId).HasColumnName("merchant_shop_id");
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.NameEn).HasMaxLength(256).HasColumnName("name_en");
            e.Property(x => x.Spec).HasMaxLength(128);
            e.Property(x => x.ImageUrl).HasMaxLength(512);
            e.Property(x => x.PlatformUrl).HasMaxLength(512);
            e.Property(x => x.WeightKg).HasColumnName("weight_kg");
            e.Property(x => x.WeightGrams).HasColumnName("weight_g");
            e.Property(x => x.LengthCm).HasColumnName("length_cm");
            e.Property(x => x.WidthCm).HasColumnName("width_cm");
            e.Property(x => x.HeightCm).HasColumnName("height_cm");
            e.Property(x => x.Link1688).HasMaxLength(512);
            e.Property(x => x.CustomerLink).HasMaxLength(512).HasColumnName("customer_link");
            e.Property(x => x.FactoryLink).HasMaxLength(512).HasColumnName("factory_link");
            e.Property(x => x.Material).HasMaxLength(128).HasColumnName("material");
            e.Property(x => x.StyleName).HasMaxLength(128).HasColumnName("style_name");
            e.Property(x => x.SizeChart).HasMaxLength(512).HasColumnName("size_chart");
            e.Property(x => x.PackageNote).HasMaxLength(512).HasColumnName("package_note");
            e.Property(x => x.SuggestedPurchasePrice).HasColumnName("suggested_purchase_price");
            e.Property(x => x.SuggestedSalePrice).HasColumnName("suggested_sale_price");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.MerchantShop).WithMany(x => x.Products).HasForeignKey(x => x.MerchantShopId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("order");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderNo);
            e.Property(x => x.MerchantShopId).HasColumnName("merchant_shop_id");
            e.Property(x => x.ConversionJobId).HasColumnName("conversion_job_id");
            e.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId);
            e.HasOne(x => x.MerchantShop).WithMany().HasForeignKey(x => x.MerchantShopId);
            e.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_item");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<Quotation>(e =>
        {
            e.ToTable("quotation");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.QuotationNo);
            e.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId);
            e.HasMany(x => x.Items).WithOne(x => x.Quotation).HasForeignKey(x => x.QuotationId);
        });

        modelBuilder.Entity<QuotationItem>(e =>
        {
            e.ToTable("quotation_item");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_order");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PurchaseNo);
            e.HasMany(x => x.Items).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId);
        });

        modelBuilder.Entity<PurchaseItem>(e =>
        {
            e.ToTable("purchase_item");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<WarehouseReceipt>(e =>
        {
            e.ToTable("warehouse_receipt");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Shipment>(e =>
        {
            e.ToTable("shipment");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Items).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId);
        });

        modelBuilder.Entity<ShipmentItem>(e =>
        {
            e.ToTable("shipment_item");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<CommissionRule>(e =>
        {
            e.ToTable("commission_rule");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<CommissionRecord>(e =>
        {
            e.ToTable("commission_record");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ExpressCompany>(e =>
        {
            e.ToTable("express_company");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Contact).HasMaxLength(256);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(x => x.CountryRates).WithOne(x => x.ExpressCompany).HasForeignKey(x => x.ExpressCompanyId);
        });

        modelBuilder.Entity<ExpressCountryRate>(e =>
        {
            e.ToTable("express_country_rate");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExpressCompanyId).HasColumnName("express_company_id");
            e.Property(x => x.CountryCode).HasMaxLength(16).HasColumnName("country_code");
            e.Property(x => x.CountryName).HasMaxLength(64).HasColumnName("country_name");
            e.Property(x => x.UnitPrice).HasColumnName("unit_price");
            e.Property(x => x.LeadDaysMin).HasColumnName("lead_days_min");
            e.Property(x => x.LeadDaysMax).HasColumnName("lead_days_max");
            e.Property(x => x.ChargeRule).HasMaxLength(64).HasColumnName("charge_rule");
            e.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            e.Property(x => x.EffectiveTo).HasColumnName("effective_to");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.ExpressCompany).WithMany(x => x.CountryRates).HasForeignKey(x => x.ExpressCompanyId);
        });

        modelBuilder.Entity<ConversionJob>(e =>
        {
            e.ToTable("conversion_job");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(256);
            e.Property(x => x.QuotationFileName).HasMaxLength(512).HasColumnName("quotation_file_name");
            e.Property(x => x.TrackingFileName).HasMaxLength(512).HasColumnName("tracking_file_name");
            e.Property(x => x.ExtractedDate).HasColumnName("extracted_date");
            e.Property(x => x.MerchantFolder).HasMaxLength(128).HasColumnName("merchant_folder");
            e.Property(x => x.MerchantId).HasColumnName("merchant_id");
            e.Property(x => x.MerchantShopId).HasColumnName("merchant_shop_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.Remark).HasMaxLength(1024);
            e.HasMany(x => x.QuotationSheets).WithOne(x => x.ConversionJob).HasForeignKey(x => x.ConversionJobId);
            e.HasMany(x => x.TrackingSheets).WithOne(x => x.ConversionJob).HasForeignKey(x => x.ConversionJobId);
        });

        modelBuilder.Entity<ConversionQuotationSheet>(e =>
        {
            e.ToTable("conversion_quotation_sheet");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConversionJobId).HasColumnName("conversion_job_id");
            e.Property(x => x.SheetName).HasMaxLength(128).HasColumnName("sheet_name");
            e.Property(x => x.SheetType).HasColumnName("sheet_type");
            e.Property(x => x.ColumnNamesJson).HasColumnName("column_names_json");
            e.Property(x => x.ContentJson).HasColumnName("content_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.ConversionJob).WithMany(x => x.QuotationSheets).HasForeignKey(x => x.ConversionJobId);
        });

        modelBuilder.Entity<ConversionTrackingSheet>(e =>
        {
            e.ToTable("conversion_tracking_sheet");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConversionJobId).HasColumnName("conversion_job_id");
            e.Property(x => x.SheetName).HasMaxLength(128).HasColumnName("sheet_name");
            e.Property(x => x.ColumnNamesJson).HasColumnName("column_names_json");
            e.Property(x => x.ContentJson).HasColumnName("content_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.ConversionJob).WithMany(x => x.TrackingSheets).HasForeignKey(x => x.ConversionJobId);
        });
    }
}
