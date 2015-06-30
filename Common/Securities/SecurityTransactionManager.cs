﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using QuantConnect.Logging;
using QuantConnect.Orders;

namespace QuantConnect.Securities 
{
    /// <summary>
    /// Algorithm Transactions Manager - Recording Transactions
    /// </summary>
    public class SecurityTransactionManager : IOrderMapping
    {
        private int _orderId = 1;
        private readonly SecurityManager _securities;
        private const decimal _minimumOrderSize = 0;
        private const int _minimumOrderQuantity = 1;
        private ConcurrentQueue<Order> _orderQueue;
        private ConcurrentQueue<OrderRequest> _orderRequestQueue;
        private ConcurrentDictionary<int, Order> _orders;
        private ConcurrentDictionary<int, List<OrderEvent>> _orderEvents;
        private Dictionary<DateTime, decimal> _transactionRecord;
        
        /// <summary>
        /// Initialise the transaction manager for holding and processing orders.
        /// </summary>
        public SecurityTransactionManager(SecurityManager security)
        {
            //Private reference for processing transactions
            _securities = security;

            //Initialise the Order Cache -- Its a mirror of the TransactionHandler.
            _orders = new ConcurrentDictionary<int, Order>();

            //Temporary Holding Queue of Orders to be Processed.
            _orderQueue = new ConcurrentQueue<Order>();

            // Internal order events storage.
            _orderEvents = new ConcurrentDictionary<int, List<OrderEvent>>();

            //Interal storage for transaction records:
            _transactionRecord = new Dictionary<DateTime, decimal>();

            _orderRequestQueue = new ConcurrentQueue<OrderRequest>();
        }

        /// <summary>
        /// Queue for holding all orders sent for processing.
        /// </summary>
        /// <remarks>Potentially for long term algorithms this will be a memory hog. Should consider dequeuing orders after a 1 day timeout</remarks>
        public ConcurrentDictionary<int, Order> _Orders 
        {
            get 
            {
                return _orders;
            }
            set
            {
                _orders = value;
            }
        }

        /// <summary>
        /// Count of currently cached orders.
        /// </summary>
        public int CachedOrderCount
        {
            get { return _orders.Count; }
        }

        /// <summary>
        /// Temporary storage for orders while waiting to process via transaction handler. Once processed they are added to the primary order queue.
        /// </summary>
        /// <seealso cref="Orders"/>
        public ConcurrentQueue<Order> OrderQueue
        {
            get
            {
                return _orderQueue;
            }
            set 
            {
                _orderQueue = value;
            }
        }

        /// <summary>
        /// Temporary storage for orders requests while waiting to process via transaction handler. Once processed, orders are updated at transaction manager.
        /// </summary>
        /// <seealso cref="Orders"/>
        public ConcurrentQueue<OrderRequest> OrderRequestQueue
        {
            get
            {
                return _orderRequestQueue;
            }
            set
            {
                _orderRequestQueue = value;
            }
        }

        /// <summary>
        /// Order event storage - a list of the order events attached to each order
        /// </summary>
        /// <remarks>Seems like a huge memory hog and may be removed, leaving OrderEvents to be disposable classes with no track record.</remarks>
        /// <seealso cref="Orders"/>
        /// <seealso cref="OrderQueue"/>
        public ConcurrentDictionary<int, List<OrderEvent>> OrderEvents
        {
            get
            {
                return _orderEvents;
            }
            set 
            {
                _orderEvents = value;
            }
        }

        /// <summary>
        /// Trade record of profits and losses for each trade statistics calculations
        /// </summary>
        public Dictionary<DateTime, decimal> TransactionRecord
        {
            get
            {
                return _transactionRecord;
            }
            set
            {
                _transactionRecord = value;
            }
        }

        /// <summary>
        /// Configurable minimum order value to ignore bad orders, or orders with unrealistic sizes
        /// </summary>
        /// <remarks>Default minimum order size is $0 value</remarks>
        public decimal MinimumOrderSize 
        {
            get 
            {
                return _minimumOrderSize;
            }
        }

        /// <summary>
        /// Configurable minimum order size to ignore bad orders, or orders with unrealistic sizes
        /// </summary>
        /// <remarks>Default minimum order size is 0 shares</remarks>
        public int MinimumOrderQuantity 
        {
            get 
            {
                return _minimumOrderQuantity;
            }
        }

        /// <summary>
        /// Get the last order id.
        /// </summary>
        public int LastOrderId
        {
            get
            {
                return _orderId;
            }
        }

        /// <summary>
        /// Add an order to collection and return the unique order id or negative if an error.
        /// </summary>
        /// <param name="order">New order object to add to processing list</param>
        /// <returns>New unique, increasing orderid</returns>
        public virtual int _AddOrder(Order order) 
        {
            try
            {
                //Ensure its flagged as a new order for the transaction handler.
                order.Id = _orderId++;
                order.Status = OrderStatus.New;
                //Add the order to the cache to monitor
                OrderQueue.Enqueue(order);
            }
            catch (Exception err)
            {
                Log.Error("Algorithm.Transaction.AddOrder(): " + err.Message);
            }
            return order.Id;
        }

        /// <summary>
        /// Add submit order request to queue and return the unique order id or negative if an error.
        /// </summary>
        /// <param name="request">Submit order request to add to processing list</param>
        /// <returns>New unique, increasing orderid</returns>
        public virtual int SubmitOrder(SubmitOrderRequest request)
        {
            try
            {
                request.OrderId = _orderId++;
                OrderRequestQueue.Enqueue(request);
            }
            catch (Exception err)
            {
                Log.Error("Algorithm.Transaction.SubmitOrder(): " + err.Message);
            }

            return request.OrderId;
        }

        /// <summary>
        /// Update an order yet to be filled such as stop or limit orders.
        /// </summary>
        /// <param name="order">Order to Update</param>
        /// <remarks>Does not apply if the order is already fully filled</remarks>
        /// <returns>
        ///     Id of the order we modified or 
        ///     -5 if the order was already filled or cancelled
        ///     -6 if the order was not found in the cache
        /// </returns>
        public int UpdateOrder(Order order)
        {
            try
            {
                //Update the order from the behaviour
                var id = order.Id;
                order.Time = _securities[order.Symbol].Time;

                //Validate order:
                if (order.Quantity == 0) return -1;

                if (_orders.ContainsKey(id))
                {
                    //-> If its already filled return false; can't be updated
                    if (_orders[id].Status == OrderStatus.Filled || _orders[id].Status == OrderStatus.Canceled)
                    {
                        return -5;
                    }

                    //Flag the order to be resubmitted.
                    order.Status = OrderStatus.Update;
                    _orders[id] = order;

                    //Send the order to transaction handler for update to be processed.
                    OrderQueue.Enqueue(order);
                } 
                else 
                {
                    //-> Its not in the orders cache, shouldn't get here
                    return -6;
                }
            } 
            catch (Exception err) 
            {
                Log.Error("Algorithm.Transactions.UpdateOrder(): " + err.Message);
                return -7;
            }
            return 0;
        }

        /// <summary>
        /// Update an order yet to be filled such as stop or limit orders.
        /// </summary>
        /// <param name="request">Update order request</param>
        /// <remarks>Does not apply if the order is already fully filled</remarks>
        /// <returns>
        ///     Id of the order we modified or 
        ///     -5 if the order was already filled or cancelled
        ///     -6 if the order was not found in the cache
        /// </returns>
        public int UpdateOrder(UpdateOrderRequest request)
        {
            try
            {
                if (request.Quantity == 0)
                    return -1;

                Order order;

                if (_orders.TryGetValue(request.OrderId, out order) == true)
                {
                    //-> If its already filled return false; can't be updated
                    if (order.Status != OrderStatus.New && order.Status != OrderStatus.Submitted)
                    {
                        return -5;
                    }

                    request.Created = _securities[order.Symbol].Time;

                    OrderRequestQueue.Enqueue(request);
                }
                else
                {
                    return -6;
                }
            }
            catch (Exception err)
            {
                Log.Error("Algorithm.Transactions.UpdateOrder(): " + err.Message);
                return -7;
            }
            return 0;
        }

        /// <summary>
        /// Added alias for RemoveOrder - 
        /// </summary>
        /// <param name="orderId">Order id we wish to cancel</param>
        public virtual void CancelOrder(int orderId)
        {
            Order order;

            if (_orders.TryGetValue(orderId, out order) == true)
            {
                if (order.Status != OrderStatus.Submitted && order.Status != OrderStatus.New)
                {
                    Log.Error("Security.TransactionManager.RemoveOutstandingOrder(): Order already filled");
                    return;
                }

                OrderRequestQueue.Enqueue(order.CancelRequest());
            }
        }

        /// <summary>
        /// Wait for a specific order to be either Filled, Invalid or Canceled
        /// </summary>
        /// <param name="orderId">The id of the order to wait for</param>
        public void WaitForOrder(int orderId)
        {
            //Wait for the market order to fill.
            //This is processed in a parallel thread.
            while (!_orders.ContainsKey(orderId) ||
                   (_orders[orderId].Status != OrderStatus.Filled &&
                    _orders[orderId].Status != OrderStatus.Invalid &&
                    _orders[orderId].Status != OrderStatus.Canceled))
            {
                Thread.Sleep(1);
            }
        }

        public void Process(Order update)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get a list of all open orders.
        /// </summary>
        /// <returns>List of open orders.</returns>
        public List<Order> GetOpenOrders()
        {
            var openOrders = (from order in _orders.Values
                where (order.Status == OrderStatus.Submitted ||
                       order.Status == OrderStatus.New)
                select order).ToList();

            return openOrders;
        }

        /// <summary>
        /// Get a list of all open orders.
        /// </summary>
        /// <returns>List of open orders.</returns>
        public List<Order> GetOrders(Predicate<Order> filter = null)
        {
            var result = (from order in _orders.Values
                              where filter == null || filter(order) == true
                              select order).ToList();

            return result;
        } 

        /// <summary>
        /// Get the order by its id
        /// </summary>
        /// <param name="orderId">Order id to fetch</param>
        /// <returns>The order with the specified id, or null if no match is found</returns>
        public Order GetOrderById(int orderId)
        {
            try
            {
                // first check the order queue
                var order = OrderQueue.FirstOrDefault(x => x.Id == orderId);
                if (order != null)
                {
                    return order;
                }
                // then check permanent storage
                if (_orders.TryGetValue(orderId, out order))
                {
                    return order;
                }
            }
            catch (Exception err)
            {
                Log.Error("TransactionManager.GetOrderById(): " + err.Message);
            }
            return null;
        }

        /// <summary>
        /// Gets the order by its brokerage id
        /// </summary>
        /// <param name="brokerageId">The brokerage id to fetch</param>
        /// <returns>The first order matching the brokerage id, or null if no match is found</returns>
        public Order GetOrderByBrokerageId(int brokerageId)
        {
            try
            {
                // first check the order queue since orders are moved from OrderQueue to Orders
                var order = OrderQueue.FirstOrDefault(x => x.BrokerId.Contains(brokerageId));
                if (order != null)
                {
                    return order;
                }

                return _orders.FirstOrDefault(x => x.Value.BrokerId.Contains(brokerageId)).Value;
            }
            catch (Exception err)
            {
                Log.Error("TransactionManager.GetOrderByBrokerageId(): " + err.Message);
                return null;
            }
        }

        /// <summary>
        /// Check if there is sufficient capital to execute this order.
        /// </summary>
        /// <param name="portfolio">Our portfolio</param>
        /// <param name="order">Order we're checking</param>
        /// <returns>True if suficient capital.</returns>
        public bool GetSufficientCapitalForOrder(SecurityPortfolioManager portfolio, Order order)
        {
            var security = _securities[order.Symbol];
            
            var freeMargin = security.MarginModel.GetMarginRemaining(portfolio, security, order.Direction);
            var initialMarginRequiredForOrder = security.MarginModel.GetInitialMarginRequiredForOrder(security, order);
            if (Math.Abs(initialMarginRequiredForOrder) > freeMargin)
            {
                Log.Error(string.Format("Transactions.GetSufficientCapitalForOrder(): Id: {0}, Initial Margin: {1}, Free Margin: {2}", order.Id, initialMarginRequiredForOrder, freeMargin));
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get a new order id, and increment the internal counter.
        /// </summary>
        /// <returns>New unique int order id.</returns>
        public int GetIncrementOrderId()
        {
            return _orderId++;
        }
    } // End Algorithm Transaction Filling Classes
} // End QC Namespace
