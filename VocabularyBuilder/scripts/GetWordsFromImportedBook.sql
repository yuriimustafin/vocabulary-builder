select Headword, SUM(Frequency) as sf, Count(*) as cnt from dbo.Words 
where Created > '2024-02-09'
Group by Headword
order by sf DESC


select * from dbo.Words 
where Created > '2024-02-09'



select count(*), 'Words' from dbo.Words 
union
select count(*), 'ImportedBookWords' from dbo.ImportedBookWords 
union
select count(*), 'BookInfo' from dbo.BookInfo 
union
select count(*), 'Chapter' from dbo.Chapter 
union
select count(*), 'Chapter' from dbo.Sense 