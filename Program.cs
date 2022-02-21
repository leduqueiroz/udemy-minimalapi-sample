using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<TodoDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo()
    {
        Description = "Todo web api implementation using Minimal Api in Asp.Net Core",
        Title = "Todo Api",
        Version = "v1",
        Contact = new OpenApiContact()
        {
            Name = "Dev's",
            Url = new Uri("https://www.udemy.com")
        }
    });
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHealthChecks().AddDbContextCheck<TodoDbContext>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo Api v1");
        c.RoutePrefix = string.Empty;
    });
}

app.MapGet("/todoitems", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http) =>
{
    using (var dbContext = dbContextFactory.CreateDbContext())
    {
        var user = http.User;
        return Results.Ok(await dbContext.TodoItems.Select(t => new TodoItemOutput(t.Title, t.IsCompleted, t.CreatedOn)).ToListAsync());
    }
}).Produces(200, typeof(List<TodoItemOutput>)).ProducesProblem(401);

app.MapGet("/todoitems/{id}", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http, int id) =>
 {
     using (var dbContext = dbContextFactory.CreateDbContext())
     {
         var user = http.User;
         return await dbContext.TodoItems.FirstOrDefaultAsync(t => t.Id == id) is TodoItem todo ? Results.Ok(todo) : Results.NotFound();
     }
 }).Produces(200, typeof(TodoItem)).ProducesProblem(401);

app.MapPost("/todoitems", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http, TodoItemInput todoItemInput) =>
 {
     using (var dbContext = dbContextFactory.CreateDbContext())
     {
         var todoItem = new TodoItem
         {
             Title = todoItemInput.Title,
             IsCompleted = todoItemInput.IsCompleted,
             CreatedOn = DateTime.Now
         };
         dbContext.TodoItems.Add(todoItem);
         await dbContext.SaveChangesAsync();
         return Results.Created($"/todoitems/{todoItem.Id}", todoItem);
     }
 }).Accepts<TodoItemInput>("application/json").Produces(201, typeof(TodoItemOutput)).ProducesProblem(401);

app.MapPut("/todoitems/{id}", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http, int id, TodoItemInput todoItemInput) =>
 {
     using (var dbContext = dbContextFactory.CreateDbContext())
     {
         if (await dbContext.TodoItems.FirstOrDefaultAsync(t => t.Id == id) is TodoItem todoItem)
         {
             todoItem.IsCompleted = todoItemInput.IsCompleted;
             await dbContext.SaveChangesAsync();
             return Results.NoContent();
         }

         return Results.NotFound();
     }
 }).Accepts<TodoItemInput>("application/json").Produces(201, typeof(TodoItemOutput)).ProducesProblem(404).ProducesProblem(401);

app.MapDelete("/todoitems/{id}", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http, int id) =>
{
    using (var dbContext = dbContextFactory.CreateDbContext())
    {
        if (await dbContext.TodoItems.FirstOrDefaultAsync(t => t.Id == id) is TodoItem todoItem)
        {
            dbContext.TodoItems.Remove(todoItem);
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        }

        return Results.NotFound();
    }
}).Accepts<TodoItemInput>("application/json").Produces(204).ProducesProblem(404).ProducesProblem(401);

app.MapGet("/health", async (HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    return report.Status == HealthStatus.Healthy ? Results.Ok(report) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).WithTags(new[] { "Health" }).Produces(200).ProducesProblem(503).ProducesProblem(401);

app.MapGet("/todoitems/history", async (IDbContextFactory<TodoDbContext> dbContextFactory, HttpContext http) =>
 {
     using (var dbContext = dbContextFactory.CreateDbContext())
     {
         return Results.Ok(await dbContext.TodoItems
            .OrderBy(todoItem => EF.Property<DateTime>(todoItem, "PeriodStart"))
            .Select(todoItem => new TodoItemAudit
            {
                Title = todoItem.Title,
                IsCompleted = todoItem.IsCompleted,
                PeriodStart = EF.Property<DateTime>(todoItem, "PeriodStart"),
                PeriodEnd = EF.Property<DateTime>(todoItem, "PeriodEnd")
            })
            .ToListAsync());
     }
 }).Produces<TodoItemAudit>(200).WithTags(new[] { "EF Core Feature" }).ProducesProblem(401);

app.Run();

public class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions<TodoDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>().ToTable("TodoItems", t => t.IsTemporal());
    }

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
}

public class TodoItem
{
    public int Id { get; set; }
    [Required]
    public string? Title { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedOn { get; set; }
}

public record TodoItemInput(string? Title, bool IsCompleted);
public record TodoItemOutput(string? Title, bool IsCompleted, DateTime? createdOn);

public class TodoItemAudit
{
    public string? Title { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}