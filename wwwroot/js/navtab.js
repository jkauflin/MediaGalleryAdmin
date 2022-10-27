/*==============================================================================
 * (C) Copyright 2022 John J Kauflin, All rights reserved.
 *----------------------------------------------------------------------------
 * DESCRIPTION:     Javascript to handle some functions associated with
 *                  bootstrap navbar and tabs, such as automatically hiding
 *                  the collapse after a nav link element is clicked, and 
 *                  programmatically handling tab activation
 *----------------------------------------------------------------------------
 * Modification History
 * 2022-06-01 JJK 	Initial version - created along with bootstrap 5 updates
 *                  and moving from JQuery to vanilla javascript
 *============================================================================*/
var navtab = (function () {
	'use strict';  // Force declaration of variables before use (among other things)

    // Keep track of the state of the navbar collapse (shown or hidden)
    var navbarCollapseShown = false;
    var collapsibleNavbar = document.getElementsByClassName("navbar-collapse")[0];
    collapsibleNavbar.addEventListener('hidden.bs.collapse', function () {
        navbarCollapseShown = false;
    })
    collapsibleNavbar.addEventListener('shown.bs.collapse', function () {
        navbarCollapseShown = true;
    })

    // Listen for nav-link clicks
    document.querySelectorAll("a.nav-link").forEach(el => el.addEventListener("click", function (event) {
        // Automatically hide the navbar collapse when an item link is clicked (and the collapse is currently shown)
        if (navbarCollapseShown) {
            new bootstrap.Collapse(document.getElementsByClassName("navbar-collapse")[0]).hide();
        }
    }));


    // Add listeners to all link-tile-tab's so click will remove the active from the current tab, 
    // show the new tab and make it active
    var linkTileTabList = document.getElementsByClassName("link-tile-tab");
    // document.querySelectorAll(".link-tile-tab").forEach(el => el.addEventListener("click", function (event) {
    for (var i = 0; i < linkTileTabList.length; i++) {
        linkTileTabList[i].addEventListener("click", function (event) {
            event.preventDefault();
            // Display the tab specified in the data-dir attribute
            displayTabPage(event.target.getAttribute("data-dir"));
        })
    }
    
    // Check if a Tab name is passed as a parameter on the URL and navigate to it
    var results = new RegExp('[\?&]tab=([^&#]*)').exec(window.location.href);
    if (results != null) {
        let tabName = results[1] || 0;
        displayTabPage(tabName);
    }

    // Function to programmically display a specified tab
    function displayTabPage(targetTab) {
        let targetTabPage = targetTab + 'Page';
        // Remove the active class on the current active tab
        document.querySelector(".nav-link.active").classList.remove("active");
        // Show the target tab page
        let targetTabElement = document.querySelector(`.navbar-nav a[href="#${targetTabPage}"]`);
        new bootstrap.Tab(targetTabElement).show();
        // Make the target tab page active (by adding the class)
        targetTabElement.classList.add("active");
    }

	//=================================================================================================================
	// This is what is exposed from this Module
	return {
	};

})(); // var main = (function(){
