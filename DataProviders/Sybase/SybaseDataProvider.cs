﻿using System;
using System.Data;
using System.Data.Linq;
using System.Linq.Expressions;
using System.Xml;
using System.Xml.Linq;

using Sybase.Data.AseClient;

namespace LinqToDB.DataProvider
{
	using Expressions;
	using Mapping;

	class SybaseDataProvider : DataProviderBase
	{
		#region Init

		public SybaseDataProvider() : base(new SybaseMappingSchema())
		{
		}

		#endregion

		#region Public Properties

		public override string Name           { get { return ProviderName.Sybase;    } }
		public override Type   ConnectionType { get { return typeof(AseConnection);  } }

		#endregion

		#region Overrides

		public override IDbConnection CreateConnection(string connectionString)
		{
			return new AseConnection(connectionString);
		}

		public override Expression ConvertDataReader(Expression reader)
		{
			return Expression.Convert(reader, typeof(AseDataReader));
		}

		static DateTime GetDateTime(IDataReader dr, int idx)
		{
			var value = dr.GetDateTime(idx);

			if (value.Year == 1900 && value.Month == 1 && value.Day == 1)
				return new DateTime(1, 1, 1, value.Hour, value.Minute, value.Second, value.Millisecond);

			return value;
		}

		public override Expression GetReaderExpression(MappingSchema mappingSchema, IDataReader reader, int idx, Expression readerExpression, Type toType)
		{
			var type = ((AseDataReader)reader).GetProviderSpecificFieldType(idx);

			if (type == typeof(DateTime))
			{
				if (toType == typeof(TimeSpan))
					return (Expression<Func<IDataReader,int,TimeSpan>>)((dr,i) => dr.GetDateTime(i) - new DateTime(1900, 1, 1));

				if (toType == typeof(DateTime))
					return (Expression<Func<IDataReader,int,DateTime>>)((dr,i) => GetDateTime(dr, i));
			}

			var expr = base.GetReaderExpression(mappingSchema, reader, idx, readerExpression, toType);

			if (expr.Type == typeof(string))
			{
				var name = ((AseDataReader)reader).GetDataTypeName(idx);
				if (name == "char" || name == "nchar")
					expr = Expression.Call(expr, MemberHelper.MethodOf<string>(s => s.Trim()));
			}

			return expr;
		}

		public override bool? IsDBNullAllowed(IDataReader reader, int idx)
		{
			var st = ((AseDataReader)reader).GetSchemaTable();
			return st == null || (bool)st.Rows[idx]["AllowDBNull"];
		}

		public override void SetParameter(IDbDataParameter parameter, string name, DataType dataType, object value)
		{
			if (dataType == DataType.Undefined && value != null)
				dataType = MappingSchema.GetDataType(value.GetType());

			switch (dataType)
			{
				case DataType.SByte      : 
					dataType = DataType.Int16;
					if (value != null)
						value = (short)(sbyte)value;
					break;

				case DataType.Time       :
					if (value is TimeSpan) value = new DateTime(1900, 1, 1) + (TimeSpan)value;
					break;

				case DataType.VarNumeric : dataType = DataType.Decimal; break;

				case DataType.Binary     :
				case DataType.VarBinary  :
					if (value is Binary) value = ((Binary)value).ToArray();
					break;

				case DataType.Xml        :
					dataType = DataType.NVarChar;
					     if (value is XDocument)   value = value.ToString();
					else if (value is XmlDocument) value = ((XmlDocument)value).InnerXml;
					break;

				case DataType.Guid       :
					if (value != null)
						value = value.ToString();
					dataType = DataType.Char;
					parameter.Size = 36;
					break;

				case DataType.Undefined  :
					if (value == null)
						dataType = DataType.Char;
					break;
			}

			base.SetParameter(parameter, "@" + name, dataType, value);
		}

		public override void SetParameterType(IDbDataParameter parameter, DataType dataType)
		{
			switch (dataType)
			{
				case DataType.UInt16        : ((AseParameter)parameter).AseDbType = AseDbType.UnsignedSmallInt; break;
				case DataType.UInt32        : ((AseParameter)parameter).AseDbType = AseDbType.UnsignedInt;      break;
				case DataType.UInt64        : ((AseParameter)parameter).AseDbType = AseDbType.UnsignedBigInt;   break;
				case DataType.Text          : ((AseParameter)parameter).AseDbType = AseDbType.Text;             break;
				case DataType.NText         : ((AseParameter)parameter).AseDbType = AseDbType.Unitext;          break;
				case DataType.Binary        : ((AseParameter)parameter).AseDbType = AseDbType.Binary;           break;
				case DataType.VarBinary     : ((AseParameter)parameter).AseDbType = AseDbType.VarBinary;        break;
				case DataType.Image         : ((AseParameter)parameter).AseDbType = AseDbType.Image;            break;
				case DataType.Money         : ((AseParameter)parameter).AseDbType = AseDbType.Money;            break;
				case DataType.SmallMoney    : ((AseParameter)parameter).AseDbType = AseDbType.SmallMoney;       break;
				case DataType.Date          : ((AseParameter)parameter).AseDbType = AseDbType.Date;             break;
				case DataType.Time          : ((AseParameter)parameter).AseDbType = AseDbType.Time;             break;
				case DataType.SmallDateTime : ((AseParameter)parameter).AseDbType = AseDbType.SmallDateTime;    break;
				case DataType.Timestamp     : ((AseParameter)parameter).AseDbType = AseDbType.TimeStamp;        break;
				default                     : base.SetParameterType(parameter, dataType);                       break;
			}
		}

		#endregion
	}
}