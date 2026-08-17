/*
 *
 *  This file is part of libfintx.
 *
 *  Copyright (C) 2016 - 2024 Torsten Klinger
 * 	E-Mail: torsten.klinger@googlemail.com
 *
 *  This program is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 3 of the License, or (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 *  Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program; if not, write to the Free Software Foundation,
 *  Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 */

using System;
using System.Collections.Generic;

namespace libfintx.FinTS.Data
{
    /// <summary>
    /// Parsed result from a DKKKU / DIKKU credit card transaction response.
    /// Balance and dates are always populated on success.
    /// Transactions are populated when the bank returns entries for the requested period.
    /// </summary>
    public class CreditCardTransactionData
    {
        /// <summary>Card number as returned by the bank (e.g. "5490027801433219").</summary>
        public string CardNumber { get; set; }

        /// <summary>
        /// Current balance. Positive = credit (in your favour). Negative = outstanding debt.
        /// Derived from the Saldo DEG in the DIKKU response: "C" sign → positive, "D" → negative.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>Currency of the balance (e.g. "EUR").</summary>
        public string BalanceCurrency { get; set; }

        /// <summary>Date the balance was last updated.</summary>
        public DateTime? BalanceDate { get; set; }

        /// <summary>Date of the last monthly card statement (Abrechnungsdatum).</summary>
        public DateTime? LastStatementDate { get; set; }

        /// <summary>
        /// Parsed transactions for the requested period. Empty when the bank returns no entries
        /// (HIRMS code 3010) or when the transaction format could not be parsed.
        /// </summary>
        public List<CreditCardTransaction> Transactions { get; set; } = new();
    }

    /// <summary>A single credit card transaction from the DIKKU response.</summary>
    public class CreditCardTransaction
    {
        /// <summary>Booking date (Buchungsdatum).</summary>
        public DateTime BookingDate { get; set; }

        /// <summary>Value date (Valutadatum). Falls back to BookingDate when not separately stated.</summary>
        public DateTime ValueDate { get; set; }

        /// <summary>
        /// Transaction amount. Positive = credit (refund/payment). Negative = debit (purchase).
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>Transaction currency (e.g. "EUR").</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>Merchant / counterparty name (col 11 of the transaction DEG).</summary>
        public string MerchantName { get; set; }

        /// <summary>Transaction reference / ID (col 22 of the transaction DEG).</summary>
        public string TransactionId { get; set; }
    }
}
