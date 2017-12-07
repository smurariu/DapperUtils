using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq.Mapping;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DapperUtils
{
    public class DapperProviderBase
    {
        private readonly string _connectionString = String.Empty;

        /// <summary>
        ///     Creates a new instance of the base provider
        /// </summary>
        /// <param name="connectionString">Connectoin string to use</param>
        public DapperProviderBase(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        ///     Connects async to the database
        /// </summary>
        /// <typeparam name="R">The return type of the method to execute</typeparam>
        /// <param name="f">Method to execute</param>
        /// <returns></returns>
        public async Task<R> ConnectAsync<R>(Func<IDbConnection, Task<R>> f)
        {
            ConfigureDapperColumnMapping<R>();

            using (IDbConnection connection = await GetOpenConnectionAsync())
            {
                return await f(connection);
            }
        }

        /// <summary>
        ///     Convert IEnumerable<T> to DataTable 
        ///     (useful when calling storeds that have table-valued parameters)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public DataTable CreateTable<T>(IEnumerable<T> collection)
        {
            // Fetch the type of List contained in the ParamValue
            var tableType = typeof(T);

            // Create DataTable which will contain data from List<T>
            var dataTable = new DataTable();

            // Fetch the Type fields count
            int columnCount = tableType.GetProperties().Count();

            var columnNameMappingDictionary = new Dictionary<string, string>();

            // Create DataTable Columns using table type field name and their types
            // Traversing through Column Collection
            for (int counter = 0; counter < columnCount; counter++)
            {
                var propertyInfo = tableType.GetProperties()[counter];

                string columnName = GetColumnAttributeValue(propertyInfo) ?? propertyInfo.Name;

                Type columnType = Nullable.GetUnderlyingType(tableType.GetProperties()[counter].PropertyType) ??
                                  tableType.GetProperties()[counter].PropertyType;

                columnNameMappingDictionary.Add(propertyInfo.Name, columnName);

                dataTable.Columns.Add(columnName, columnType);
            }

            // Return parameter with null value
            if (collection == null)
                return dataTable;

            // Traverse through number of entries / rows in the List
            foreach (var item in collection)
            {
                // Create a new DataRow
                DataRow dataRow = dataTable.NewRow();

                // Traverse through type fields or column names
                for (int counter = 0; counter < columnCount; counter++)
                {
                    // Fetch Column Name
                    string columnName = columnNameMappingDictionary[tableType.GetProperties()[counter].Name];

                    //Fetch Value for each column for each element in the List<T>
                    dataRow[columnName] = item
                        .GetType().GetProperties()[counter]
                        .GetValue(item) ?? DBNull.Value;
                }
                // Add Row to Table
                dataTable.Rows.Add(dataRow);
            }

            return (dataTable);
        }

        /// <summary>
        ///     Executes work transactionally
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="work">The work to be executed</param>
        /// <param name="isolationLevel">The IsolationLevel to use. Default set to ReadCommitted.</param>
        /// <returns></returns>
        public async Task<T> Transactionally<T>(IDbConnection connection, Func<IDbTransaction, Task<T>> work,
           IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            using (IDbTransaction transaction = connection.BeginTransaction(isolationLevel))
            {
                try
                {
                    return await work(transaction);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        #region Private Methods

        private async Task<IDbConnection> GetOpenConnectionAsync()
        {
            var dbConnection = new SqlConnection(_connectionString);

            await dbConnection.OpenAsync();

            return dbConnection;
        }

        private string GetColumnAttributeValue(PropertyInfo propertyInfo)
        {
            var columnAttribute = propertyInfo.GetCustomAttributes(false).OfType<ColumnAttribute>().FirstOrDefault();
            return columnAttribute?.Name;
        }

        private void ConfigureDapperColumnMapping<T>()
        {
            ConfigureDapperColumnMapping(typeof(T));
        }

        private void ConfigureDapperColumnMapping(Type propertyType)
        {
            Type[] t = { propertyType };

            if (propertyType.IsGenericType)
            {
                t = propertyType.GetGenericArguments();
            }

            Func<Type, string, PropertyInfo> mapping = (type, columnName) =>
                type.GetProperties().FirstOrDefault(prop => GetColumnAttributeValue(prop) == columnName);

            for (int i = 0; i < t.Length; i++)
            {
                SqlMapper.SetTypeMap(t[i], new CustomPropertyTypeMap(t[i], mapping));

                //map properties that are not value types
                foreach (var property in t[i].GetProperties(BindingFlags.Instance
                                                          | BindingFlags.NonPublic
                                                          | BindingFlags.Public))
                {
                    if (property.PropertyType.IsValueType == false)
                    {
                        //WARNING: Infinite recursion is possible if you have circular references
                        ConfigureDapperColumnMapping(property.PropertyType);
                    }
                }
            }
        }

        #endregion
    }
}
