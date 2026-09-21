**LibraSys: A Secure Library Management System with Database Security and Role-Based Access Control** 

1. **System Overview** 

**System Description :**

LibraSys is a secure web-based library management system that supports catalog browsing, book management, borrowing and returning, reservations, member management, penalty and fine management, and library reporting. It provides role-based access for four roles: **Guest**, **Member**, **Librarian**, and **Database Administrator** (**DBA**), with each role having defined access to library and database functions.

The system protects sensitive member information through column-level encryption and implements password hashing, role-based access control, database roles and privileges, views, schema-level permissions, **GRANT**, **REVOKE**, **GRANT OPTION**, cascading privilege revocation, and audit logging to ensure controlled and accountable access to library data. It also records penalties for overdue, lost, or damaged books and manages corresponding payments to maintain accurate and transparent penalty records.

**Scope:**  
The general objective of *LibraSys: A Secure Library Management System with Database Security and Role-Based Access Control* is to develop a secure web-based library management system that streamlines catalog browsing, book management, borrowing, returning, reservations, penalty and fine management, and library reporting while implementing database security and role-based authorization.

Specifically, the system aims to:

1. Develop a secure authentication and user management system using password hashing and role-based access control.  
2. Provide Guests and Members with an organized catalog for searching and viewing available books.  
3. Implement borrowing, returning, and reservation functions that track book copies, due dates, borrowing history, and overdue transactions.  
4. implement query tuning mechanisms to minimize execution latency and optimize resource utilization across diverse workloads.   
5. Implement a penalty and fine management function that records penalties for overdue, lost, or damaged books and tracks their corresponding payment status and payment records.  
6. Provide Librarians with tools to manage books, book copies, members, borrowing transactions, reservations, penalties, payments, and library operations.  
7. Provide Librarians with reports and authorized database views for monitoring library activities and generating library statistics.  
8. Protect sensitive member information through column-level encryption and controlled access to sensitive data.  
9. Implement database security mechanisms, including database roles, privileges, **GRANT**, **REVOKE**, **GRANT OPTION**, schema-level permissions, and view-based authorization.  
10. Implement audit logging to record important user, library, and administrative activities for accountability and monitoring.  
11. Maintain a centralized and organized database to support accurate library records, penalty and payment tracking, and efficient library operations.

**Limitations:**

1. **Limited to Basic Library Operations**  
   The system focuses on core library functions such as catalog browsing, book management, borrowing, returning, reservations, member management, penalty and fine management, and library reporting. Advanced services such as interlibrary loans, book acquisitions, and digital library management are **outside the scope of the system.**  
2. **Physical Books Only**  
   The system manages physical books and their copies. It does not provide online reading, downloading, or management of digital books.  
3. **Limited Notification System**  
   The system records due dates and overdue transactions but does not provide automated SMS, email, or push notifications.  
4. **Single Library Branch**  
   The system is designed for a single library branch and does not support the management of multiple library branches.  
5. **Manual Book and Member Management**  
   Librarians are responsible for entering and updating book and member information. The system does not automatically retrieve book information from external library databases.  
6. **Report Scope**  
   Reports focus on basic library statistics, including borrowing activity, book availability, overdue transactions, member borrowing history, and penalty records. Advanced analytics and predictive reporting are outside the scope of the system.  
7. **Limited Penalty and Payment Management**  
   The system records penalties for applicable overdue, lost, or damaged books and tracks corresponding payment records and payment status. However, it does not provide advanced accounting functions, automated payment processing, online payment gateway integration, or comprehensive financial management.  
8. **Database Dependency**  
   The system requires an available database connection for authentication, catalog access, borrowing, reservations, penalty and payment records, and other database-dependent functions.  
9. **Limited Security Scope**  
   The system demonstrates selected database security mechanisms, including password hashing, role-based access control, database privileges, views, schema-level permissions, and column-level encryption. It does not attempt to implement every cybersecurity mechanism or function as a complete enterprise security system.  
10. **Web-Based Access Only**  
    The system is developed as a web application and does not include separate native Android, iOS, or desktop applications.  
      
2. **User Roles and Access Control**   
   1. **Guest** 

   Allowed:

* Browse and search the library catalog  
* View available books  
* View basic book information  
* View general library information  
    
  Restricted:  
* Borrow, return, or reserve books  
* View borrowing history or member information  
* Manage books, book copies, or members  
* Access transaction logs  
* Manage roles or database privileges


  2. **Member** 

		Allowed:

* Search and view available books  
* Borrow and return books  
* Make and manage reservations  
* View personal borrowing history  
* View personal penalties and fines   
* Manage personal account information  
    
  Restricted:  
* Manage books, book copies, or other members  
* View other members' personal, penalty, or payment information   
* Manage or modify penalties and fines   
* Modify payment records   
* Access transaction logs  
* Manage roles or database privileges  
* Access database security configuration

  3. **Librarian**   
     Allowed:  
* Manage books and book copies  
* Manage member records  
* Manage borrowing, returning, and reservations  
* Manage penalties and fines for applicable overdue, lost, or damaged books   
* Record and manage member payments for penalties and fines   
* View library transactions and activity records  
* Generate library reports  
* Access authorized database views  
* Manage authorized application-level library settings  
    
  Restricted:  
* Create or manage database users and roles  
* Grant or revoke database privileges  
* Manage database encryption  
* Manage schemas or schema-level permissions  
* Modify database security policies  
* Override DBA-controlled security settings

  4. **DBA**   
     Allowed:  
* Manage database users and roles  
* Grant and revoke database privileges  
* Manage schemas and schema-level permissions  
* Configure and manage column-level encryption  
* Manage database views and authorization  
* Configure database security policies  
* Maintain audit and security mechanisms  
* Demonstrate **GRANT OPTION** and cascading **REVOKE**  
* Enforce database-level access-control policies  
    
  Restricted:  
* None at the database administration level 


  References for roles:

  [https://pdfs.semanticscholar.org/5ea1/86ead524ad74443eb80c92c2ed1888720186.pdf](https://pdfs.semanticscholar.org/5ea1/86ead524ad74443eb80c92c2ed1888720186.pdf)

	[https://dl.acm.org/doi/epdf/10.1145/3326467.3326473](https://dl.acm.org/doi/epdf/10.1145/3326467.3326473)

3. **Database Design**   
   1. **Entity Relation Ship Diagram**  
        
      

     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
4. **Database Security**   
5. **User Interface** 