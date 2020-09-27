﻿using Dapper;
using Data.Core;
using Domain.Core;
using Domain.Core.Entities;
using Domain.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;

namespace Data.Store.SqlServer
{
	public class UsersRepository : IUsersRepository
	{
		internal readonly IDbConnectionFactory _dbConnectionFactory = null;

		private readonly Func<UserEntity, BlobEntity, UserEntity> _userMap = (user, blob) =>
		{
			user.PictureInfo = blob?.GetInfo();
			return user;
		};

		/// <summary>
		/// Contructor method.
		/// </summary>
		public UsersRepository(IDbConnectionFactory dbConnectionFactory)
		{
			_dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory), nameof(UsersRepository));
		}

		public UserEntity Create(UserEntity userEntity)
		{
			userEntity.SecurityStamp = Guid.NewGuid().ToString();

			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				connection.Open();

				using (var transaction = connection.BeginTransaction())
				{
					var parameters = new DynamicParameters();

					parameters.Add("@LockoutEnabled", userEntity.LockoutEnabled, DbType.Boolean);
					parameters.Add("@EmailConfirmed", userEntity.EmailConfirmed, DbType.Boolean);
					parameters.Add("@PasswordHash", userEntity.PasswordHash, DbType.String);
					parameters.Add("@SecurityStamp", userEntity.SecurityStamp, DbType.String);
					parameters.Add("@UserName", userEntity.UserName, DbType.String);
					parameters.Add("@FullName", userEntity.FullName, DbType.String);
					parameters.Add("@Email", userEntity.Email, DbType.String);

					userEntity.Id = connection.ExecuteScalar<int>(
						sql: "UsersManagementInsert",
						commandType: CommandType.StoredProcedure,
						transaction: transaction,
						param: parameters
					);

					UserClaimsSet(connection, transaction, claims: userEntity.Claims, user: userEntity, cleanup: false);
					UserRealmsSet(connection, transaction, realms: userEntity.Realms, user: userEntity, cleanup: false);
					UserRolesSet(connection, transaction, roles: userEntity.Roles, user: userEntity, cleanup: false);

					transaction.Commit();

					return userEntity;
				}
			}
		}

		public IEnumerable<UserEntity> GetAll()
		{
			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				var parameters = new DynamicParameters();

				using (var reader = connection.QueryMultiple(sql: "UsersManagementSelect", commandType: CommandType.StoredProcedure, param: parameters))
				{
					return BuildEntities(reader);
				}
			}
		}

		public UserEntity GetByEmail(string userEmail)
		{
			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				var parameters = new DynamicParameters();

				parameters.Add("@Email", userEmail, DbType.String);

				using (var reader = connection.QueryMultiple(sql: "UsersManagementSelect", commandType: CommandType.StoredProcedure, param: parameters))
				{
					return BuildEntity(reader);
				}
			}
		}

		public UserEntity GetById(int userId)
		{
			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				var parameters = new DynamicParameters();

				parameters.Add("@Id", userId, DbType.Int32);

				using (var reader = connection.QueryMultiple(sql: "UsersManagementSelect", commandType: CommandType.StoredProcedure, param: parameters))
				{
					return BuildEntity(reader);
				}
			}
		}

		public UserEntity GetByUserName(string userName)
		{
			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				var parameters = new DynamicParameters();

				parameters.Add("@UserName", userName, DbType.String);

				using (var reader = connection.QueryMultiple(sql: "UsersManagementSelect", commandType: CommandType.StoredProcedure, param: parameters))
				{
					return BuildEntity(reader);
				}
			}
		}

		public void Update(UserEntity userEntity)
		{
			userEntity.SecurityStamp = Guid.NewGuid().ToString();

			using (var connection = _dbConnectionFactory.CreateConnection())
			{
				connection.Open();

				using (var transaction = connection.BeginTransaction())
				{
					var parameters = new DynamicParameters();

					parameters.Add("@Id", userEntity.Id, DbType.Int32);
					parameters.Add("@IsBlocked", userEntity.IsBlocked, DbType.Boolean);
					parameters.Add("@SecurityStamp", userEntity.SecurityStamp, DbType.String);

					int result = connection.Execute(
						sql: "UsersManagementUpdate",
						commandType: CommandType.StoredProcedure,
						transaction: transaction,
						param: parameters
					);

					UserClaimsSet(connection, transaction, claims: userEntity.Claims, user: userEntity, cleanup: true);
					UserRealmsSet(connection, transaction, realms: userEntity.Realms, user: userEntity, cleanup: true);
					UserRolesSet(connection, transaction, roles: userEntity.Roles, user: userEntity, cleanup: true);

					transaction.Commit();
				}
			}
		}

		private IEnumerable<UserEntity> BuildEntities(SqlMapper.GridReader reader)
		{
			var entities = reader.Read(_userMap).ToList();

			var claimsCollection = reader
				.Read<UserClaim>()
				.GroupBy(uClaim => uClaim.UserId)
				.ToDictionary(group => group.Key, group => group.ToList());

			var realmsCollection = reader
				.Read<UserRealm>()
				.GroupBy(uRealm => uRealm.UserId)
				.ToDictionary(group => group.Key, group => group.ToList());

			var rolesCollection = reader
				.Read<UserRole>()
				.GroupBy(uRole => uRole.UserId)
				.ToDictionary(group => group.Key, group => group.ToList());

			foreach (var entity in entities)
			{
				List<UserClaim> claims;
				List<UserRealm> realms;
				List<UserRole> roles;

				if (!claimsCollection.TryGetValue(entity.Id, out claims))
				{
					claims = new List<UserClaim>();
				}

				if (!realmsCollection.TryGetValue(entity.Id, out realms))
				{
					realms = new List<UserRealm>();
				}

				if (!rolesCollection.TryGetValue(entity.Id, out roles))
				{
					roles = new List<UserRole>();
				}

				entity.Claims = claims.Select(claim => new Claim(claim.Type, claim.Value)).ToList();
				entity.Realms = realms.Select(realm => realm.Realm).ToList();
				entity.Roles = roles.Select(role => role.Role).ToList();
			}

			return entities;
		}

		private UserEntity BuildEntity(SqlMapper.GridReader reader)
		{
			var entity = reader.Read(_userMap).SingleOrDefault();

			if (entity != null)
			{
				var claims = reader.Read<UserClaim>().ToList();
				var realms = reader.Read<UserRealm>().ToList();
				var roles = reader.Read<UserRole>().ToList();

				entity.Claims = claims.Select(claim => new Claim(claim.Type, claim.Value)).ToList();
				entity.Realms = realms.Select(realm => realm.Realm).ToList();
				entity.Roles = roles.Select(role => role.Role).ToList();
			}

			return entity;
		}

		private void UserClaimsSet(IDbConnection dbConnection, IDbTransaction transaction, List<Claim> claims, UserEntity user, bool cleanup)
		{
			if (cleanup)
			{
				var parameters = new DynamicParameters();

				parameters.Add(@"UserId", user.Id, DbType.Int32);

				dbConnection.Execute(
					sql: "DELETE FROM [UserClaims] WHERE UserId = @UserId",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}

			foreach (var claim in claims)
			{
				var parameters = new DynamicParameters();

				parameters.Add("@UserId", user.Id, DbType.Int32);
				parameters.Add("@Type", claim.Type, DbType.String);
				parameters.Add("@Value", claim.Value, DbType.String);

				dbConnection.Query(
					sql: "INSERT INTO [UserClaims] (UserId, [Type], [Value]) VALUES (@UserId, @Type, @Value)",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}
		}

		private void UserRealmsSet(IDbConnection dbConnection, IDbTransaction transaction, List<Realm> realms, UserEntity user, bool cleanup)
		{
			if (cleanup)
			{
				var parameters = new DynamicParameters();

				parameters.Add(@"UserId", user.Id, DbType.Int32);

				dbConnection.Execute(
					sql: "DELETE FROM [UserRealms] WHERE UserId = @UserId",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}

			foreach (var realm in realms)
			{
				var parameters = new DynamicParameters();

				parameters.Add("@UserId", user.Id, DbType.Int32);
				parameters.Add("@Realm", realm, DbType.Int32);

				dbConnection.Query(
					sql: "INSERT INTO [UserRealms] (UserId, [Realm]) VALUES (@UserId, @Realm)",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}
		}

		private void UserRolesSet(IDbConnection dbConnection, IDbTransaction transaction, List<Role> roles, UserEntity user, bool cleanup)
		{
			if (cleanup)
			{
				var parameters = new DynamicParameters();

				parameters.Add(@"UserId", user.Id, DbType.Int32);

				dbConnection.Execute(
					sql: "DELETE FROM [UserRoles] WHERE UserId = @UserId",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}

			foreach (var role in roles)
			{
				var parameters = new DynamicParameters();

				parameters.Add("@UserId", user.Id, DbType.Int32);
				parameters.Add("@Role", role.ToString(), DbType.String);

				dbConnection.Query(
					sql: "INSERT INTO [UserRoles] (UserId, [Role]) VALUES (@UserId, @Role)",
					commandType: CommandType.Text,
					transaction: transaction,
					param: parameters
				);
			}
		}
	}
}
