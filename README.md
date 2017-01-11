# DapperUtils

Bits of code to make working with dapper more enjoyable.

## DapperProviderBase

Dapper provider base is a base class that contains a few useful methods when creating 
Dapper-based providers. Dapper allows you to execute queries against sql server and maps 
the results to .NET objects.  

Examples below:

## 1) Retrieval of a entity using a stored procedure

```cs
public class DomainSpecifficReadProvider : DapperProviderBase 
{
    ...

    public async Task<DomainEntity> GetDomainEntityAsync(string domainEntityId)
    {
        string command = "EXEC GET_DOMAIN_ENTITY_BY_ID @DOMAIN_ENTITY_ID";
        var parameters = new DynamicParameters();
        parameters.Add("DOMAIN_ENTITY_ID", domainEntityId);

        var cd = new CommandDefinition(command, parameters);
        IEnumerable<DomainEntityReadModel> domainEntities = await ConnectAsync(c => c.QueryAsync<DomainEntityReadModel>(cd));
        
        ...
    }
    ...
}
```

The relevant properties of this entity must be decorated with the ```ColumnAttribute``` like in the example below:

```cs
    using System.Data.Linq.Mapping;

    class DomainEntityReadModel
    {
        [Column(Name = "DOMAIN_ENTITY_ID")]
        public int DomainEntityId { get; set; }

        [Column(Name = "DOMAIN_ENTITY_NAME")]
        public string DomainEntityName { get; set; }

        [Column(Name = "DOMAIN_ENTITY_DESCRIPTION")]
        public string DomainEntityDescription { get; set; }

        [Column(Name = "DOMAIN_ENTITY_CODE")]
        public string DomainEntityCode { get; set; }
    }

``` 

Needles to say that the names you specify here must match the column names returned by your database query.

## 2) Passing table-valued parameters to a query

```cs

public class CustomersWriteProvider : DapperProviderBase 
{
    ...
    public async Task<int> CreateCustomerAsync(Customer customer)
    {
        DataTable phones = CreateTable(customer.Phones.Select(c => new PhoneWriteModel { PhoneTypeId = (int)c.Type, PhoneNumber = c.Number }));

        string command = "EXEC CREATE_CUSTOMER @FIRSTNAME, @LASTNAME, @EMAIL, @Phones, @CUSTOMER_ID OUTPUT";

        DynamicParameters dp = new DynamicParameters();
        dp.Add("FIRSTNAME", customer.FirstName);
        dp.Add("LASTNAME", customer.LastName);
        dp.Add("EMAIL", customer.Email);

   x    dp.Add("Phones", phones.AsTableValuedParameter("PhonesTableType"));

        dp.Add("CUSTOMER_ID", 0, DbType.Int32, ParameterDirection.Output);

        var cd = new CommandDefinition(command, dp);

        int affectedRows = await ConnectAsync(c => c.ExecuteAsync(cd));
        customer.Id = dp.Get<int>("CUSTOMER_ID");

        return customer.Id;
    }
    ...
}

```
As you can see on the lines marked with an ```x``` the parameters are added "```AsTableValuedParameter```" and here you must pass the exact name you used in sql server to define your parameter table type.

The ```PhoneWriteModel``` has to match the structure of the parameter table type you have in sql server:

```cs
    class PhoneWriteModel
    {
        [Column(Name = "PHONE_ID")]
        public int PhoneId { get; set; }

        [Column(Name = "PHONE_NUMBER")]
        public string PhoneNumber { get; set; }

        [Column(Name = "PHONE_TYPE_ID")]
        public int PhoneTypeId { get; set; }
    }
```

Happy coding.