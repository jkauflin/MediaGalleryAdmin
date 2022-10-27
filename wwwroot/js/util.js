/*==============================================================================
 * (C) Copyright 2015,2016,2017,2022 John J Kauflin, All rights reserved. 
 *----------------------------------------------------------------------------
 * DESCRIPTION:  Common JS functions for any web app to augment bootstrap
 *               displays and form field/update functions
 *----------------------------------------------------------------------------
 * Modification History
 * 2015-03-06 JJK 	Initial version 
 * 2015-04-09 JJK   Added Regular Expressions and functions for validating
 * 					email addresses and replacing non-printable characters
 * 2016-05-18 JJK   Added setTextArea
 * 2016-08-14 JJK   Imported data from Access backup of 8/12/2016
 * 2016-08-26 JJK   Went live, and Paypal payments working in Prod!!!
 * 2018-10-14 JJK   Re-factored for modules
 * 2018-10-27 JJK   Modified getJSONfromInputs to just loop through the DIV
 *                  looking for input fields, and added an action parameter
 * 2018-10-28 JJK   Went back to declaring variables in the functions
 * 2018-11-01 JJK   Modified getJSONfromInputs to only include elements with
 *                  an Id and check for checkbox "checked"
 * 2018-12-19 JJK   Added functions to abstract screen activities such as
 *                  display, search, edit, and update
 * 2019-09-28 JJK   Modified the JSON inputs method to accept DEV object
 *                  or name string.  Modified the AJAX calls to use new
 *                  promises to check result
 * 2022-05-15 JJK   Updating for bootstrap 5 and Fetch (and to be a good
 *                  general set of utility function for any web app).
 *                  Removed email address regedit (bootstrap 5 validates)
 *                  Removed cursor stuff
 * 2022-05-18 JJK   Added 2 functions (updateJSONfromInputs, updateTEXTfromInputs)
 *                  to handle Update requests to services using Fetch with POST 
 *                  and JSON structure in Request body.  Makes use of the existing
 *                  getJSONfromInputs function, and it does the thing I have
 *                  always wanted to which is to parse the response to see if
 *                  it is JSON or non-JSON if there was some kind of Exception.
 *                  *** Philosophy is to NOT count on elements in the UI DOM,
 *                  but to pass elements to functions if we want them to set
 *                  display elements in the DOM.  The update functions will
 *                  write details to the console.log and pop-up alerts if
 *                  a display element is not specified.
 *                  Added fetchData along the same pattern lines
 * 2022-05-25 JJK   Created getParamDatafromInputs for both JSON and url
 *                  param string and implemented in fetch and update
 *                  (Changed getJSONfromInputs to call new getParamDatafromInputs)
 * 2022-05-30 JJK   Implemented a tradition (err,data) and callback for the 
 *                  fetchData and getJSONfromInputs functions, and updated for 
 *                  better error handling (the Catch catches error in the 
 *                  main callback function after the fetch).
 *                  Moved display functions back to the main callback function
 *============================================================================*/
 var util = (function(){
    'use strict';  // Force declaration of variables before use (among other things)
    //=================================================================================================================
    // Private variables for the Module

    //=================================================================================================================
    // Variables cached from the DOM

    //=================================================================================================================
    // Bind events

    //=================================================================================================================
    // Module methods
    function currTime() {
        const options = {
            //timeZone: "Africa/Accra",
            //hour12: true,
            //hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        };

        var tempDate = new Date();
        return tempDate.toLocaleTimeString("en-US", options);
    }

    function sleep(milliseconds) {
        var start = new Date().getTime();
        for (var i = 0; i < 1e7; i++) {
            if ((new Date().getTime() - start) > milliseconds) {
                break;
            }
        }
    }

    function urlParam(name) {
        var results = new RegExp('[\?&]' + name + '=([^&#]*)').exec(window.location.href);
        if (results == null) {
            return null;
        }
        else {
            return results[1] || 0;
        }
    }
    /*
    example.com?param1=name&param2=&id=6
        urlParam('param1');     // name
        urlParam('id');         // 6
        rlParam('param2');      // null
    */

    // Non-Printable characters - Hex 01 to 1F, and 7F
    var nonPrintableCharsStr = "[\x01-\x1F\x7F]";
    // "g" global so it does more than 1 substitution
    var regexNonPrintableChars = new RegExp(nonPrintableCharsStr, "g");
    function cleanStr(inStr) {
        return inStr.replace(regexNonPrintableChars, '');
    }

    // Filter out commas (for CSV outputs)
    var commaHexStr = "[\x2C]";
    var regexCommaHexStr = new RegExp(commaHexStr, "g");
    function csvFilter(inVal) {
        return inVal.toString().replace(regexCommaHexStr, '');
    }

    //Replace every ascii character except decimal and digits with a null, and round to 2 decimal places
    var nonMoneyCharsStr = "[\x01-\x2D\x2F\x3A-\x7F]";
    //"g" global so it does more than 1 substitution
    var regexNonMoneyChars = new RegExp(nonMoneyCharsStr, "g");
    function formatMoney(inAmount) {
        var inAmountStr = '' + inAmount;
        inAmountStr = inAmountStr.replace(regexNonMoneyChars, '');
        return parseFloat(inAmountStr).toFixed(2);
    }

    function formatDate(inDate) {
        var tempDate = inDate;
        if (tempDate == null) {
            tempDate = new Date();
        }
        var tempMonth = tempDate.getMonth() + 1;
        if (tempDate.getMonth() < 9) {
            tempMonth = '0' + (tempDate.getMonth() + 1);
        }
        var tempDay = tempDate.getDate();
        if (tempDate.getDate() < 10) {
            tempDay = '0' + tempDate.getDate();
        }
        return tempDate.getFullYear() + '-' + tempMonth + '-' + tempDay;
    }

    function formatDatetime(inDate) {
        var td = inDate;
        if (td == null) {
            td = new Date();
        }
        var formattedDate = formatDate(td);
        return `${formattedDate} ${td.getHours()}:${td.getMinutes()}:${td.getSeconds()}.${td.getMilliseconds()}`;
    }

    // Helper functions for setting UI components from data
    function setBoolText(inBool) {
        var tempStr = "NO";
        if (inBool) {
            tempStr = "YES";
        }
        return tempStr;
    }
    function setCheckbox(checkVal) {
        var tempStr = '';
        if (checkVal == 1) {
            tempStr = 'checked=true';
        }
        return '<input type="checkbox" ' + tempStr + ' disabled="disabled">';
    }
    //function setCheckboxEdit(checkVal, idName) {
    function setCheckboxEdit(idName, checkVal) {
        var tempStr = '';
        if (checkVal == 1) {
            tempStr = 'checked=true';
        }
        return '<input id="' + idName + '" type="checkbox" ' + tempStr + '>';
    }

/* Bootstrap 5 form input fields
            <form id="InputValues" action="#">
                <div class="row">
                    <div class="col p-0">
                        <div class="form-floating m-1">
                            <input id="targetTemperature" type="number" class="form-control">
                            <label for="targetTemperature">Target temp</label>
                        </div>
                        <div class="form-floating m-1">
                            <input id="desc" type="text" class="form-control">
                            <label for="desc">Description</label>
                        </div>
                        <div class="form-floating m-1">
                            <input id="germinationDate" type="date" class="form-control">
                            <label for="germinationDate">Germination date</label>
                        </div>
                    </div>
                </div><!-- end of row -->
            </form>
*/
    function setInputText(idName, textVal, textSize) {
        return '<input id="' + idName + '" name="' + idName + '" type="text" class="form-control input-sm resetval" value="' + textVal + '" size="' + textSize + '" maxlength="' + textSize + '">';
    }
    function setTextArea(idName, textVal, rows) {
        return '<textarea id="' + idName + '" class="form-control input-sm" rows="' + rows + '">' + textVal + '</textarea>';
    }
    function setInputDate(idName, textVal, textSize) {
        return '<input id="' + idName + '" type="text" class="form-control input-sm Date" value="' + textVal + '" size="' + textSize + '" maxlength="' + textSize + '" placeholder="YYYY-MM-DD">';
    }
    function setSelectOption(optVal, displayVal, selected, bg) {
        var tempStr = '';
        if (selected) {
            tempStr = '<option class="' + bg + '" value="' + optVal + '" selected>' + displayVal + '</option>';
        } else {
            tempStr = '<option class="' + bg + '" value="' + optVal + '">' + displayVal + '</option>';
        }
        return tempStr;
    }



    //=============================================================================================
    // Function to get all input objects within a DIV, and extra entries from a map
    // and construct output string with names and values (in JSON or param string)
    // 2018-08-31 JJK - Modified to check for input DIV name string or object
    // 2022-05-25 JJK - Added option for JSON or url param string
    // Parameters:
    //   formName - Form name which includes input fields to include in query
    //   paramMap - Structure holding extra parameters to include
    //   jsonOutput - true or false (false means url param string starting with ?)
    //=============================================================================================
    function getParamDatafromInputs(formName, paramMap=null, jsonOutput=true) {
        let first = true;
        let paramData = '';
        let paramSeperator = '';
        if (jsonOutput) {
            paramSeperator = ',';
        } else {
            paramSeperator = ';';
        }

        // Check if the input form is specified
        if (typeof formName !== "undefined" && formName !== null) {
            // Get all the input objects within the Form
            const inputForm = document.getElementById(formName);
            if (inputForm !== null) {
                // document.getElementsByClassName("form-control") - could use form-control class???
                paramData = addParamDataForTag(inputForm, paramData, paramSeperator, first, jsonOutput, 'input');
                paramData = addParamDataForTag(inputForm, paramData, paramSeperator, first, jsonOutput, 'textarea');
                paramData = addParamDataForTag(inputForm, paramData, paramSeperator, first, jsonOutput, 'select');
            }
        }

        if (paramMap !== null) {
            paramMap.forEach(function (value, key) {
                if (first) {
                    first = false;
                    if (jsonOutput) {
                        paramData = '{';
                    } else {
                        paramData = '?';
                    }
                } else {
                    paramData += paramSeperator;
                }
                if (jsonOutput) {
                    paramData += '"' + key + '" : "' + value + '"';
                } else {
                    paramData += key + '=' + value;
                }
            });
        }

        if (jsonOutput) {
            paramData += '}';
        }

        return paramData;
    }

    function addParamDataForTag(inputForm, paramData, paramSeperator, first, jsonOutput, tagName) {
        let inputElementList = inputForm.getElementsByTagName(tagName);
        for (let i=0; i < inputElementList.length; i++) {
            // Only include elements that have an id
            if (inputElementList[i].id === null) {
                continue;
            }

            if (first) {
                first = false;
                if (jsonOutput) {
                    paramData = '{';
                } else {
                    paramData = '?';
                }
            } else {
                paramData += paramSeperator;
            }

            //console.log(`id = ${inputElementList[i].id}, type = ${inputElementList[i].type}, value = ${inputElementList[i].value}, checked = ${inputElementList[i].checked}`)
            if (inputElementList[i].type === "checkbox") {
                if (inputElementList[i].checked) {
                    if (jsonOutput) {
                        paramData += '"' + inputElementList[i].id + '" : 1';
                    } else {
                        paramData += inputElementList[i].id + '=1';
                    }
            
                } else {
                    if (jsonOutput) {
                        paramData += '"' + inputElementList[i].id + '" : 0';
                    } else {
                        paramData += inputElementList[i].id + '=0';
                    }
                }
            } else {
                if (jsonOutput) {
                    paramData += '"' + inputElementList[i].id + '" : "' + cleanStr(inputElementList[i].value) + '"';
                } else {
                    paramData += inputElementList[i].id + '=' + cleanStr(inputElementList[i].value);
                }
            }
        }

        return paramData;
    }

    //=============================================================================================
    // 2022-05-25 JJK   Modified the original function to call the new function with the default
    //                  of JSON = true
    // *** Get rid of this when everything has been re-factored to call the new function ***
    //=============================================================================================
    function getJSONfromInputs(formName, paramMap) {
        return getParamDatafromInputs(formName, paramMap);
    }


    function log(inStr) {
        console.log(formatDatetime + " " + inStr);
    }
    
    //=================================================================================================================
    // This is what is exposed from this Module
    return {
        sleep,
        urlParam,
        cleanStr,
        csvFilter,
        formatMoney,
        formatDate,
        formatDate,
        formatDatetime,
        setBoolText,
        setCheckbox,
        setCheckboxEdit,
        setInputText,
        setTextArea,
        setInputDate,
        setSelectOption,
        getParamDatafromInputs,
        getJSONfromInputs,
        log
    };
        
})(); // var util = (function(){
