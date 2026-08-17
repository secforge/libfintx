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
using System.Text;
using System.Threading.Tasks;
using libfintx.FinTS.Data;
using libfintx.FinTS.Message;
using libfintx.FinTS.Segments;
using Microsoft.Extensions.Logging;

namespace libfintx.FinTS
{
    /// <summary>
    /// DKKKU – Kreditkartenumsätze anfordern (DK extension, credit card transactions).
    /// Wire format (v2): DKKKU:&lt;seg&gt;:2+&lt;subAcct&gt;+&lt;cardNr&gt;+&lt;bookingStatus&gt;+&lt;vonDatum&gt;+&lt;bisDatum&gt;+&lt;maxEntries&gt;+&lt;Aufsetzpunkt&gt;'
    /// Confirmed from StarMoney trace: DKKKU:3:2++5490027801433219++20260226+20260226'
    /// </summary>
    public static class DKKKU
    {
        /// <summary>
        /// Request credit card transactions via DKKKU (v2).
        /// The card number is taken from <see cref="FinTsClient.activeAccount"/>.AccountNumber.
        /// </summary>
        public static async Task<string> Init_DKKKU(FinTsClient client, string fromDate, string toDate, string startpoint)
        {
            client.Logger.LogInformation("Starting job DKKKU: Request credit card transactions");

            var connectionDetails = client.ConnectionDetails;
            AccountInformation activeAccount;
            if (client.activeAccount != null)
                activeAccount = client.activeAccount;
            else
                activeAccount = new AccountInformation()
                {
                    AccountNumber = connectionDetails.Account,
                    SubAccountFeature = connectionDetails.SubAccount,
                };

            activeAccount.AccountNumber ??= string.Empty;
            activeAccount.SubAccountFeature ??= string.Empty;

            client.SEGNUM = Convert.ToInt16(SEG_NUM.Seg3);

            SEG sEG = new SEG();
            StringBuilder sb = new StringBuilder();

            // DE1: Sub-account feature / Kartenfolgenummer (usually empty for credit cards)
            sb.Append(activeAccount.SubAccountFeature);
            sb.Append(sEG.Delimiter);
            // DE2: Kreditkartennummer (card number, e.g. 5490027801433219)
            sb.Append(activeAccount.AccountNumber);

            if (!string.IsNullOrEmpty(fromDate) || !string.IsNullOrEmpty(startpoint))
            {
                // DE3: Buchungsstatus – empty = all (booked + pending)
                sb.Append(sEG.Delimiter);
                // DE4: vonDatum
                sb.Append(sEG.Delimiter);
                sb.Append(fromDate ?? string.Empty);
                // DE5: bisDatum
                sb.Append(sEG.Delimiter);
                sb.Append(toDate ?? string.Empty);

                if (!string.IsNullOrEmpty(startpoint))
                {
                    // DE6: Maximale Anzahl (empty)
                    sb.Append(sEG.Delimiter);
                    // DE7: Aufsetzpunkt
                    sb.Append(sEG.Delimiter);
                    sb.Append(startpoint);
                }
            }

            sb.Append(sEG.Terminator);

            string segments = sEG.toSEG(new SEG_DATA
            {
                Header = "DKKKU",
                Num = client.SEGNUM,
                Version = 2,
                RefNum = 0,
                RawData = sb.ToString()
            });

            if (client.BPD?.IsTANRequired("DKKKU") == true)
            {
                client.SEGNUM = Convert.ToInt16(SEG_NUM.Seg4);
                segments = HKTAN.Init_HKTAN(client, segments, "DKKKU");
            }

            string message = FinTSMessage.Create(client, client.HNHBS, client.HNHBK, segments, client.HIRMS);
            string response = await FinTSMessage.Send(client, message);

            client.Parse_Message(response);

            return response;
        }
    }
}
