using (var context = new AppDbContext())
{
    var pipeline = new Pipeline(context);
    pipeline.Run();
}

Console.WriteLine("ETL terminado");
