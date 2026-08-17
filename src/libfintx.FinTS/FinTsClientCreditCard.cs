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
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using libfintx.FinTS.Data;
using Microsoft.Extensions.Logging;

namespace libfintx.FinTS
{
    public partial class FinTsClient
    {
        /// <summary>
        /// Request credit card transactions via DKKKU (DK extension segment).
        /// Set <see cref="activeAccount"/> to the credit card account before calling:
        /// AccountNumber = card number (e.g. "5490027801433219"), no IBAN required.
        /// </summary>
        /// <param name="tanDialog">TAN dialog for SCA.</param>
        /// <param name="startDate">Optional start of the date range.</param>
        /// <param name="endDate">Optional end of the date range.</param>
        /// <returns>
        /// Parsed DIKKU response including current balance and typed transaction list.
        /// Transactions is empty when the bank reports no entries for the period (code 3010).
        /// </returns>
        public async Task<HBCIDialogResult<CreditCardTransactionData>> CreditCardTransactions(
            TANDialog tanDialog, DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = await InitializeConnection();
            if (result.HasError)
                return result.TypedResult<CreditCardTransactionData>();

            result = await ProcessSCA(result, tanDialog);
            if (result.HasError)
                return result.TypedResult<CreditCardTransactionData>();

            string fromDateStr = startDate?.ToString("yyyyMMdd");
            string toDateStr = endDate?.ToString("yyyyMMdd");

            string bankCode = await Transaction.DKKKU(this, fromDateStr, toDateStr, null);
            result = new HBCIDialogResult(Parse_BankCode(bankCode), bankCode);
            if (result.HasError)
                return result.TypedResult<CreditCardTransactionData>();

            result = await ProcessSCA(result, tanDialog);
            if (result.HasError)
                return result.TypedResult<CreditCardTransactionData>();

            bankCode = result.RawData;
            var allTransactionDegs = new List<string>();

            // Collect first page's transactions
            CollectDikkuTransactionDegs(bankCode, allTransactionDegs);

            // Paginate via HIRMS 3040
            string bankCode_ = bankCode;
            while (bankCode_.Contains("+3040::"))
            {
                Parse_Message(bankCode_);
                string startpoint = Parse_Transactions_Startpoint(bankCode_);

                bankCode_ = await Transaction.DKKKU(this, fromDateStr, toDateStr, startpoint);
                result = new HBCIDialogResult(Parse_BankCode(bankCode_), bankCode_);
                if (result.HasError)
                    return result.TypedResult<CreditCardTransactionData>();

                result = await ProcessSCA(result, tanDialog);
                if (result.HasError)
                    return result.TypedResult<CreditCardTransactionData>();

                bankCode_ = result.RawData;
                CollectDikkuTransactionDegs(bankCode_, allTransactionDegs);
            }

            var data = ParseDikkuResponse(bankCode, allTransactionDegs);
            return result.TypedResult(data);
        }

        /// <summary>
        /// Extracts transaction DEG strings from a DIKKU segment in the response.
        /// DIKKU format (Sparkasse/KSK):
        ///   field[0] = cardNr
        ///   field[1] = cardSeq (empty)
        ///   field[2] = Saldo DEG "C/D:amount:currency:date"
        ///   field[3] = last statement date
        ///   field[4] = empty placeholder
        ///   field[5..n] = transaction DEGs (colon-separated, ~25 columns each)
        /// </summary>
        private static void CollectDikkuTransactionDegs(string response, List<string> target)
        {
            foreach (var rawSeg in Helper.SplitEncryptedSegments(response))
            {
                if (!rawSeg.StartsWith("DIKKU"))
                    continue;

                int firstPlus = rawSeg.IndexOf('+');
                if (firstPlus < 0)
                    continue;

                var fields = SplitOnUnescapedDelimiter(rawSeg[(firstPlus + 1)..], '+');

                // fields[5] and above are transaction DEGs
                for (int i = 5; i < fields.Count; i++)
                {
                    if (!string.IsNullOrEmpty(fields[i]))
                        target.Add(fields[i]);
                }
                break; // only one DIKKU per response
            }
        }

        /// <summary>
        /// Parses the balance/date header from the first DIKKU segment and builds the result.
        /// </summary>
        private static CreditCardTransactionData ParseDikkuResponse(string response, List<string> transactionDegs)
        {
            var data = new CreditCardTransactionData();

            foreach (var rawSeg in Helper.SplitEncryptedSegments(response))
            {
                if (!rawSeg.StartsWith("DIKKU"))
                    continue;

                int firstPlus = rawSeg.IndexOf('+');
                if (firstPlus < 0)
                    break;

                var fields = SplitOnUnescapedDelimiter(rawSeg[(firstPlus + 1)..], '+');

                // field[0] = card number
                data.CardNumber = fields.Count > 0 ? Unescape(fields[0]) : null;

                // field[2] = Saldo DEG: "C:0,:EUR:20260226" or "D:123,45:EUR:20260226"
                if (fields.Count > 2)
                {
                    var saldo = SplitOnUnescapedDelimiter(fields[2], ':');
                    string sign = saldo.Count > 0 ? saldo[0] : "C";
                    if (saldo.Count > 1)
                    {
                        string amtStr = saldo[1].Replace(',', '.');
                        if (decimal.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amt))
                            data.Balance = sign == "D" ? -amt : amt;
                    }
                    if (saldo.Count > 2)
                        data.BalanceCurrency = Unescape(saldo[2]);
                    if (saldo.Count > 3 && DateTime.TryParseExact(saldo[3], "yyyyMMdd",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime bd))
                        data.BalanceDate = bd;
                }

                // field[3] = last statement date
                if (fields.Count > 3 && DateTime.TryParseExact(fields[3].Trim(), "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime sd))
                    data.LastStatementDate = sd;

                break;
            }

            data.Transactions = ParseTransactionDegs(transactionDegs);
            return data;
        }

        /// <summary>
        /// Parses transaction DEG strings into CreditCardTransaction records.
        /// Column layout (Sparkasse/KSK, confirmed from KskFints DkkkuParser):
        ///   0  = card number
        ///   1  = booking date (YYYYMMDD)
        ///   2  = value date   (YYYYMMDD)
        ///   3  = empty
        ///   4  = amount (German decimal, e.g. "29,6")
        ///   5  = currency
        ///   6  = direction ("D"=debit / "C"=credit)
        ///   11 = merchant / description text
        ///   22 = transaction reference ID
        /// </summary>
        private static List<CreditCardTransaction> ParseTransactionDegs(List<string> degs)
        {
            var result = new List<CreditCardTransaction>(degs.Count);
            foreach (var deg in degs)
            {
                var tx = TryParseTxDeg(deg);
                if (tx != null)
                    result.Add(tx);
            }
            return result;
        }

        private static CreditCardTransaction TryParseTxDeg(string deg)
        {
            var cols = SplitOnUnescapedDelimiter(deg, ':');
            if (cols.Count < 7)
                return null;

            if (!TryParseDate(cols[1], out DateTime bookingDate))
                return null;

            TryParseDate(cols[2], out DateTime valueDate);
            if (valueDate == default)
                valueDate = bookingDate;

            string amtStr = cols.Count > 4 ? cols[4] : "";
            if (!TryParseGermanDecimal(amtStr, out decimal amount))
                return null;

            string currency = cols.Count > 5 ? Unescape(cols[5]).Trim() : "EUR";
            string direction = cols.Count > 6 ? cols[6].Trim() : "D";
            if (direction == "D")
                amount = -amount;

            string merchant = cols.Count > 11 ? Unescape(cols[11]).Trim() : "";
            string txId = cols.Count > 22 ? Unescape(cols[22]).Trim() : "";

            return new CreditCardTransaction
            {
                BookingDate = bookingDate,
                ValueDate = valueDate,
                Amount = amount,
                Currency = string.IsNullOrEmpty(currency) ? "EUR" : currency,
                MerchantName = string.IsNullOrEmpty(merchant) ? null : merchant,
                TransactionId = string.IsNullOrEmpty(txId) ? null : txId,
            };
        }

        private static bool TryParseDate(string s, out DateTime date)
        {
            s = s?.Trim() ?? "";
            if (DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
            if (DateTime.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
            date = default;
            return false;
        }

        private static bool TryParseGermanDecimal(string s, out decimal value)
        {
            // German format: "29,6" or "1.234,56" → remove thousand separator, replace comma
            s = s?.Trim().Replace(".", "").Replace(",", ".") ?? "";
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Splits a FinTS field string on the given delimiter, respecting '?' escaping.
        /// Does not handle @len@ binary blocks (not present in DIKKU transaction DEGs).
        /// </summary>
        private static List<string> SplitOnUnescapedDelimiter(string s, char delimiter)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            bool escaped = false;

            foreach (char ch in s)
            {
                if (escaped)
                {
                    current.Append(ch);
                    escaped = false;
                    continue;
                }
                if (ch == '?')
                {
                    escaped = true;
                    continue; // drop the escape character itself
                }
                if (ch == delimiter)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(ch);
            }
            parts.Add(current.ToString());
            return parts;
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.Contains('?'))
                return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool escaped = false;
            foreach (char ch in s)
            {
                if (!escaped && ch == '?') { escaped = true; continue; }
                sb.Append(ch);
                escaped = false;
            }
            return sb.ToString();
        }
    }
}
