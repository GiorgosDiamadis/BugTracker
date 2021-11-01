using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Core.Flash;
using Microsoft.AspNetCore.Components.Forms;
using MySqlConnector;
using WebApplication.Database.DatabaseAccessObjects.Interfaces;
using WebApplication.Models;
using RestSharp;
using RestSharp.Authenticators;
using WebApplication.Models.DataTransferObjects;
using WebApplication.Services.Interfaces;

namespace WebApplication.Database.DatabaseAccessObjects
{
    public class UserDbAccess : IDatabaseAccessObject<DataTransferObjectBase>
    {
        private readonly MySqlContext _mySqlContext;
        private readonly int NAME_ROLE_CHAR_LENGTH = 120;

        private readonly int PASSWORD_LENGTH = 64;

        public UserDbAccess(MySqlContext mySqlContext)
        {
            this._mySqlContext = mySqlContext;
        }

        public IEnumerable<DataTransferObjectBase> GetAll(Predicate<DataTransferObjectBase> condition = null)
        {
            return null;
        }

        public async void CreateTableIfNotExists()
        {
            Console.WriteLine("Creating users table");
            MySqlConnection connection = _mySqlContext.GetConnection();
            await connection.OpenAsync();
            MySqlCommand mySqlCommand =
                new MySqlCommand(
                    @"create table if not exists users(
                    id varchar(120) unique not null,
                    has_validated int not null,
                    email_confirmation_token varchar(120) not null,
                     password_recovery_token varchar(120) not null   ,
                     user_name varchar(120) unique not null,
                    user_role varchar(120) not null,
                    user_phone varchar(32) not null,
                    user_email varchar(64) not null ,
                    user_salt varchar(120) not null, 
                    user_hash varchar(120) not null ,
                    primary key(id));",
                    connection);

            await mySqlCommand.ExecuteReaderAsync();
            await connection.CloseAsync();
        }

        public async Task<Messenger> Insert(Dictionary<string, object> parameters)
        {
            string userId = Guid.NewGuid().ToString();

            string emailConfirmationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            MySqlConnection connection = _mySqlContext.GetConnection();
            await connection.OpenAsync();
            MySqlCommand mySqlCommand =
                new MySqlCommand(
                    @"insert into users(
                  id,
                  has_validated,
                  email_confirmation_token,
                  password_recovery_token,
                  user_name,
                  user_role,
                  user_phone,
                  user_email,
                  user_salt,
                  user_hash)
                values(
                       @ID,
                       @HASVALIDATED,
                       @CONFIRMATIONTOKEN,
                       @RECOVERYTOKEN,
                       @NAME,
                       @ROLE,
                       @PHONE,
                       @EMAIL,
                       @SALT,
                       @HASH) ",
                    connection);

            mySqlCommand.Parameters.AddWithValue("@ID", userId);
            mySqlCommand.Parameters.AddWithValue("@HASVALIDATED", 0);
            mySqlCommand.Parameters.AddWithValue("@CONFIRMATIONTOKEN", emailConfirmationToken);
            mySqlCommand.Parameters.AddWithValue("@RECOVERYTOKEN", "");
            mySqlCommand.Parameters.AddWithValue("@NAME", parameters["name"].ToString());
            mySqlCommand.Parameters.AddWithValue("@ROLE", (int) parameters["role"]);
            mySqlCommand.Parameters.AddWithValue("@PHONE", parameters["phone"].ToString());
            mySqlCommand.Parameters.AddWithValue("@EMAIL", parameters["email"].ToString());
            mySqlCommand.Parameters.AddWithValue("@SALT", parameters["salt"].ToString());
            mySqlCommand.Parameters.AddWithValue("@HASH", parameters["hash"].ToString());


            try
            {
                await mySqlCommand.ExecuteReaderAsync();
                await connection.CloseAsync();
                User newUser = new User();
                newUser.Email = parameters["email"].ToString();
                newUser.ConfirmationToken = emailConfirmationToken;

                Messenger messenger =
                    new Messenger(
                        "You have been successfully registered. Please confirm your email in order to proceed!", false);

                messenger.SetData(newUser);
                return messenger;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await connection.CloseAsync();
                return new Messenger("Username or email already exists.", true);
            }
        }

        public async Task<Messenger> ConfirmEmail(string token)
        {
            MySqlConnection connection = _mySqlContext.GetConnection();
            await connection.OpenAsync();
            try
            {
                MySqlCommand mySqlCommand =
                    new MySqlCommand(
                        @"update users set has_validated=1 where email_confirmation_token=@TOKEN",
                        connection);
                mySqlCommand.Parameters.AddWithValue("@TOKEN", token);
                MySqlDataReader reader = await mySqlCommand.ExecuteReaderAsync();
                await connection.CloseAsync();
                if (reader.RecordsAffected != 0)
                {
                    return new Messenger("Your email has been confirmed! You can now log in to your account.", false);
                }
                else
                {
                    return new Messenger("Invalid token.", true);
                }
            }
            catch (Exception e)
            {
                await connection.CloseAsync();
                Console.WriteLine(e);
                return new Messenger("Something went wrong during the confirmation process. Please try again later",
                    true);
            }
        }


        public async Task<Messenger> Get(string name = null, string id = null)
        {
            MySqlConnection connection = _mySqlContext.GetConnection();
            await connection.OpenAsync();
            MySqlCommand mySqlCommand;
            if (id == null)
            {
                mySqlCommand =
                    new MySqlCommand(
                        "select * from users where user_name=@NAME;",
                        connection);
                mySqlCommand.Parameters.AddWithValue("@NAME", name);
            }
            else
            {
                mySqlCommand =
                    new MySqlCommand(
                        "select * from users where id=@ID;",
                        connection);
                mySqlCommand.Parameters.AddWithValue("@ID", id);
            }

            MySqlDataReader reader = await mySqlCommand.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                User dbUser = new User();
                while (reader.Read())
                {
                    dbUser.Name = reader.GetString(reader.GetOrdinal("user_name"));
                    dbUser.Hash = reader.GetString(reader.GetOrdinal("user_hash"));
                    dbUser.Salt = reader.GetString(reader.GetOrdinal("user_salt"));
                    dbUser.PhoneNumber = reader.GetString(reader.GetOrdinal("user_phone"));
                    dbUser.Email = reader.GetString(reader.GetOrdinal("user_email"));
                    int enumPos = Int32.Parse(reader.GetString(reader.GetOrdinal("user_role")));
                    dbUser.Role = (Role) enumPos;
                    dbUser.HasValidated = reader.GetInt32(reader.GetOrdinal("has_validated"));
                    dbUser.Id = reader.GetString(reader.GetOrdinal("id"));
                }

                await connection.CloseAsync();
                Messenger result = new Messenger("", false);
                result.SetData(dbUser);

                return result;
            }
            else
            {
                Messenger result = new Messenger("Username or password is incorrect!", true);
                await connection.CloseAsync();
                return result;
            }
        }

        public async Task<Messenger> Edit(Dictionary<string, KeyValuePair<object, Type>> parameters,
            Dictionary<string, KeyValuePair<object, Type>> where)
        {
            string query = "update users set ";
            foreach (var kv in parameters)
            {
                query += kv.Key + "=";
                if (kv.Value.Value == typeof(string))
                {
                    query += $"'{kv.Value.Key}',";
                }
                else
                {
                    query += (int) kv.Value.Key + ",";
                }
            }

            query = query.Remove(query.Length - 1, 1);
            query += " where ";

            foreach (var kv in where)
            {
                query += kv.Key + "=";
                if (kv.Value.Value == typeof(string))
                {
                    query += $"'{kv.Value.Key}' and ";
                }
                else
                {
                    query += (int) kv.Value.Key + " and ";
                }
            }

            query = query.Remove(query.Length - 5, 4);
            
            MySqlConnection connection = _mySqlContext.GetConnection();
            await connection.OpenAsync();
            MySqlCommand mySqlCommand = new MySqlCommand(query, connection);


            try
            {
                await mySqlCommand.ExecuteReaderAsync();
                await connection.CloseAsync();

                return new Messenger("", false);
            }
            catch (Exception e)
            {
                await connection.CloseAsync();

                return new Messenger("", true);
            }
            
        }

        public bool Remove(DataTransferObjectBase data)
        {
            return false;
        }
    }
}