using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;

namespace MQRabbit
{
    public class MqManager : IDisposable
	{
		private Dictionary<string, object> _args;

		private readonly int _xMaxLength = int.Parse(ConfigurationManager.AppSettings["x-max-length"]);

		private readonly int _xMessageTtl = int.Parse(ConfigurationManager.AppSettings["x-message-ttl"]);

		private readonly int _xExpires = int.Parse(ConfigurationManager.AppSettings["x-expires"]);

		protected IModel Channel
		{
			get;
			set;
		}

		protected IConnection Connection
		{
			get;
			set;
		}

		private RabbitMQ.Client.ConnectionFactory ConnectionFactory
		{
			get;
			set;
		}

		public string ExchangeTypeName
		{
			get;
			set;
		}

		private MqManager()
		{
			ExchangeTypeName = "topic";
			if (_args == null)
			{
				_args = new Dictionary<string, object>()
				{
					{ "x-max-length", _xMaxLength },
					{ "x-message-ttl", _xMessageTtl },
					{ "x-expires", _xExpires }
				};
			}
		}

		public MqManager(RmqInitModel mqInitModel) : this()
		{
			ConnectionFactory = new RabbitMQ.Client.ConnectionFactory()
			{
				HostName = mqInitModel.HostName,
				Port = mqInitModel.Port,
				UserName = mqInitModel.UserName,
				Password = mqInitModel.Password,
				AutomaticRecoveryEnabled = mqInitModel.AutomaticRecoveryEnabled,
				RequestedHeartbeat = mqInitModel.RequestedHeartbeat,
				NetworkRecoveryInterval = mqInitModel.NetworkRecoveryInterval
			};
		}

		public MqManager(string rabbitHost, int rabbitPort, string rabbitUserName, string rabbitPassword) : this()
		{
			ConnectionFactory = new RabbitMQ.Client.ConnectionFactory()
			{
				HostName = rabbitHost,
				Port = rabbitPort,
				UserName = rabbitUserName,
				Password = rabbitPassword,
				AutomaticRecoveryEnabled = true,
				RequestedHeartbeat = 30,
				NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
			};
		}

		private void Channel_BasicAcks(object sender, BasicAckEventArgs e)
		{
		}

		private void Channel_BasicNacks(object sender, BasicNackEventArgs e)
		{
		}

		private bool Connect()
		{
			bool flag;
			try
			{
				if (Connection == null || !Connection.IsOpen)
				{
					Connection = ConnectionFactory.CreateConnection();
					Channel = Connection.CreateModel();
				}
				else if (Channel.IsClosed)
				{
					Channel = Connection.CreateModel();
				}
				flag = true;
			}
			catch (BrokerUnreachableException)
			{
				flag = false;
			}
			catch (EndOfStreamException)
			{
				flag = false;
			}
			return flag;
		}

		public void Consume(Exchanges exchangeName, Action<object, string> messageHandler, params string[] bindingKeys)
		{
			if (!Connect())
			{
				throw new Exception("Connection failed!");
			}

			string exName = exchangeName.ToString();
			Channel.ExchangeDeclare(exName, ExchangeTypeName, false, false, null);
			string queueName = $"{exchangeName.ToString()}_{string.Join("_", bindingKeys)}_{Guid.NewGuid()}";
			queueName = (queueName.Length > 255 ? queueName.Substring(0, 254) : queueName);
			var queue = Channel.QueueDeclare(queueName, false, false, true, _args);
			string[] strArrays = bindingKeys;

			for (int i = 0; i < (int)strArrays.Length; i++)
			{
				string bindingKey = strArrays[i];
				Channel.QueueBind(queue.QueueName, exName, bindingKey, null);
			}

			var consumer = new EventingBasicConsumer(Channel);
			Channel.ContinuationTimeout = TimeSpan.FromSeconds(20);
			consumer.Received += (model, ea) => messageHandler(model, Encoding.UTF8.GetString(ea.Body));
			Channel.BasicConsume(queue.QueueName, true, consumer);
		}

		public void Consume(string exchangeName, Action<object, string> messageHandler, params string[] bindingKeys)
		{
			if (!Connect())
			{
				throw new Exception("Connection failed!");
			}
			Channel.ExchangeDeclare(exchangeName, ExchangeTypeName, false, false, null);
			string queueName = $"{exchangeName}_{string.Join("_", bindingKeys)}_{Guid.NewGuid()}";
			queueName = (queueName.Length > 255 ? queueName.Substring(0, 254) : queueName);
			QueueDeclareOk queue = Channel.QueueDeclare(queueName, false, false, true, _args);
			string[] strArrays = bindingKeys;
			for (int i = 0; i < (int)strArrays.Length; i++)
			{
				string bindingKey = strArrays[i];
				Channel.QueueBind(queue.QueueName, exchangeName, bindingKey, null);
			}
			EventingBasicConsumer consumer = new EventingBasicConsumer(Channel);
			consumer.Received += (model, ea) => messageHandler(model, Encoding.UTF8.GetString(ea.Body));
			Channel.BasicConsume(queue.QueueName, true, consumer);
		}

		public void Disconnect()
		{
			if (Connection != null && Connection.IsOpen)
			{
				Connection.Close();
			}
		}

		public void Dispose()
		{
			IConnection connection = Connection;
			if (connection != null)
			{
				connection.Close();
			}

		    IModel channel = Channel;
			if (channel != null)
			{
				channel.Dispose();
			}

		    IConnection connection1 = Connection;
			if (connection1 == null)
			{
				return;
			}

			connection1.Dispose();
		}

	    public void Produce(Exchanges exchangeName, string message, string bindingKey)
	    {
	        if (!Connect())
	        {
	            throw new Exception("Connection failed!");
	        }

	        var exName = exchangeName.ToString();
	        if (!string.IsNullOrEmpty(exName))
	        {
	            Channel.ExchangeDeclare(exName, ExchangeTypeName, false, false, null);
	        }

	        var properties = Channel.CreateBasicProperties();
	        properties.DeliveryMode = 2;

	        properties.ContentType = "application/json";
	        var binaryMessage = Encoding.UTF8.GetBytes(message);

	        lock (Channel)
	        {
	            Channel.BasicPublish(exName, bindingKey, properties, binaryMessage);
	        }
	    }

	    public void Produce(string exchangeName, string message, string bindingKey)
		{
			if (!Connect())
			{
				throw new Exception("Connection failed!");
			}

			if (!string.IsNullOrEmpty(exchangeName))
			{
				Channel.ExchangeDeclare(exchangeName, ExchangeTypeName, false, false, null);
			}

			var properties = Channel.CreateBasicProperties();
			properties.DeliveryMode = 2;
			properties.ContentType = "application/json";

			var binaryMessage = Encoding.UTF8.GetBytes(message);

			lock (Channel)
			{
				Channel.BasicPublish(exchangeName, bindingKey, properties, binaryMessage);
			}
		}
	}
}