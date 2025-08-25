# Requirements Traceability Matrix

| Req | Implementation (Planned) | Status |
| --- | ------------------------ | ------ |
| FR001 Store Customer Profiles | Table: Customers (Partition: Region or first letter, Row: CustomerID); Repo: ICustomerRepository & AzureTableCustomerRepository | Planned |
| FR002 Store Product Information | Table: Products; dynamic columns for flexible attributes | Planned |
| FR003 Host Product Images | Blob Container: product-images; service IImageStorageService | Planned |
| FR004 Generate Image Thumbnails | Function: ImageThumbnailFunction triggered on product-images uploads, writes to product-thumbnails | Planned |
| FR005 Queue Order Processing Tasks | Queue: new-orders; service IOrderQueueService | Planned |
| FR006 Queue Inventory Update Tasks | Queue: inventory-updates; service IInventoryQueueService | Planned |
| FR007 Process Queued Orders | Function: OrderProcessorFunction dequeues new-orders | Planned |
| FR008 Log Application Events | Azure Files share: logs via IAppLogger abstraction writing to mounted path | Planned |
| FR009 Access Log Files Securely | Documented mounting instructions in README/logging.md | Planned |
| FR010 Retrieve Order Status | MVC Controller: OrderStatusController querying Tables & queue metadata | Planned |
| FR011 Ensure Data Resilience | Storage accounts configured with GRS (deployment infra note) | Planned |
| FR012 Manage Storage Lifecycle | Blob lifecycle management policy (infra-as-code placeholder) | Planned |
